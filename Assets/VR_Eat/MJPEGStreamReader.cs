using System;
using System.Collections;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

public class MJPEGStreamReader : MonoBehaviour
{
    public string streamURL = "http://10.100.170.102:5000/video_feed";
    public Vector2Int textureSize = new Vector2Int(1280, 720);
    public MeshRenderer displayRenderer;
    [Range(0, 1)]
    public float threshold = 0.2f; // インスペクターで調整可能に

    [Header("Server Connection")]
    [Tooltip("サーバー起動待機時間（秒）")]
    public float serverStartupDelay = 5.0f;
    [Tooltip("接続リトライ間隔（秒）")]
    public float retryInterval = 2.0f;
    [Tooltip("最大リトライ回数（0で無制限）")]
    public int maxRetryCount = 10;

    private Texture2D tex;
    private bool isTextureReady = false;
    private Material materialInstance;


    public Texture2D LatestTexture => tex;
    public bool IsTextureReady => isTextureReady;


    void OnEnable()
    {
        CreateTexture();
        StartCoroutine(ReceiveMJPEG());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (tex != null)
        {
            Destroy(tex);
            tex = null;
        }
        if (materialInstance != null)
        {
            Destroy(materialInstance);
            materialInstance = null;
        }
    }

    void CreateTexture()
    {
        if (tex != null)
        {
            Destroy(tex);
        }

        // テクスチャフォーマットをRGB24に変更
        tex = new Texture2D(textureSize.x, textureSize.y, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        // 初期化時に緑色のテクスチャを設定（デバッグ用）
        Color[] colors = new Color[textureSize.x * textureSize.y];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.green;
        }
        tex.SetPixels(colors);
        tex.Apply();

        if (displayRenderer != null)
        {
            if (materialInstance == null)
            {
                materialInstance = new Material(displayRenderer.material);
                displayRenderer.material = materialInstance;
            }

            // クロマキーの色を調整（より明るい緑に）
            materialInstance.SetColor("_ChromaColor", new Color(0.0f, 1.0f, 0.0f, 1.0f));
            materialInstance.SetFloat("_Threshold", threshold);
            materialInstance.SetColor("_Color", Color.white);
            materialInstance.SetTexture("_MainTex", tex);
        }
        else
        {
            Debug.LogError("DisplayRenderer is not assigned!");
        }
    }

    void Update()
    {
        // 実行時にthresholdを動的に調整
        if (materialInstance != null && Mathf.Abs(materialInstance.GetFloat("_Threshold") - threshold) > 0.001f)
        {
            materialInstance.SetFloat("_Threshold", threshold);
        }
    }

    IEnumerator ReceiveMJPEG()
    {
        // サーバー起動を待機してから接続開始
        if (serverStartupDelay > 0f)
        {
            Debug.Log($"[MJPEG] サーバー起動待機中... ({serverStartupDelay}秒)");
            yield return new WaitForSeconds(serverStartupDelay);
        }

        int retryCount = 0;
        while (maxRetryCount <= 0 || retryCount < maxRetryCount)
        {
            yield return StartCoroutine(ReceiveMJPEGInternal());
            retryCount++;

            if (maxRetryCount > 0)
            {
                Debug.LogWarning($"[MJPEG] 接続リトライ ({retryCount}/{maxRetryCount})... {retryInterval}秒後に再接続");
            }
            else
            {
                Debug.LogWarning($"[MJPEG] 接続リトライ ({retryCount})... {retryInterval}秒後に再接続");
            }

            yield return new WaitForSeconds(retryInterval);
        }

        Debug.LogError($"[MJPEG] 最大リトライ回数 ({maxRetryCount}) に到達。接続を中止します。");
    }

    private IEnumerator ReceiveMJPEGInternal()
    {
        HttpWebRequest req = null;
        WebResponse resp = null;
        Stream stream = null;
        MemoryStream imgStream = null;

        try
        {
            req = (HttpWebRequest)WebRequest.Create(streamURL);
            req.Timeout = 10000;
            resp = req.GetResponse();
            stream = resp.GetResponseStream();
            imgStream = new MemoryStream();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MJPEG] 接続エラー: {e.Message}");
            yield break; // 接続エラーの場合は終了
        }

        if (stream == null)
        {
            Debug.LogError("ストリームが取得できませんでした");
            yield break;
        }

        byte[] buffer = new byte[102400];
        bool readingImage = false;

        while (true)
        {
            if (tex == null)
            {
                CreateTexture();
            }

            int bytesRead = 0;
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MJPEG] ストリーム読み込みエラー: {e.Message}");
                yield break; // 読み込みエラーの場合は終了
            }

            if (bytesRead == 0) break;

            for (int i = 0; i < bytesRead; i++)
            {
                if (!readingImage && i < bytesRead - 1 && buffer[i] == 0xFF && buffer[i + 1] == 0xD8)
                {
                    imgStream.SetLength(0);
                    imgStream.WriteByte(buffer[i]);
                    i++;
                    imgStream.WriteByte(buffer[i]);
                    readingImage = true;
                    //Debug.Log("[MjpegDebug] JPEG開始マーカー検出");
                }
                else if (readingImage)
                {
                    imgStream.WriteByte(buffer[i]);

                    if (i < bytesRead - 1 && buffer[i] == 0xFF && buffer[i + 1] == 0xD9)
                    {
                        imgStream.WriteByte(buffer[i + 1]);
                        i++;

                        byte[] jpgBytes = imgStream.ToArray();
                        //Debug.Log($"[MjpegDebug] JPEG切り出し: {jpgBytes.Length} bytes");

                        if (tex != null)
                        {
                            bool loadSuccess = tex.LoadImage(jpgBytes);
                            if (loadSuccess)
                            {
                                isTextureReady = true;
                                if (materialInstance != null)
                                {
                                    tex.Apply(false);
                                    materialInstance.SetTexture("_MainTex", tex);
                                }
                            }
                            else
                            {
                                Debug.LogError("テクスチャの読み込みに失敗しました");
                                CreateTexture();
                            }
                        }

                        readingImage = false;
                        yield return null;
                    }
                }
            }
            yield return null;
        }

        // リソースのクリーンアップ
        try
        {
            if (imgStream != null) imgStream.Dispose();
            if (stream != null) stream.Dispose();
            if (resp != null) resp.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"リソースクリーンアップエラー: {e.Message}");
        }
    }
}
