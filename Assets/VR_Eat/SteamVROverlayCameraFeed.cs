// SteamVROverlayCameraFeed.cs
// 必要: SteamVR Unity Plugin (Valve.VR 名前空間)
using UnityEngine;
using Valve.VR; // OpenVR

public class SteamVROverlayCameraFeed : MonoBehaviour
{
    public MJPEGStreamReader stream;       // MJPEG受信用コンポーネント
    public Material chromaBlitMaterial;    // "Hidden/ChromaKeyToAlphaBlit" シェーダを適用したマテリアル
    public Color chromaColor = Color.green; // Python 側 /set_color に対応
    [Range(0, 1)] public float threshold = 0.20f;
    public float widthInMeters = 0.6f;     // Overlay の物理サイズ（m）
    public Vector3 hmdOffset = new Vector3(0f, -0.25f, -1.2f); // HMD位置基準のオフセット

    // ワールド固定（Absolute）用の位置・回転・原点
    public Vector3 worldPosition = new Vector3(0f, 1.2f, -2f);
    public Vector3 worldEuler = Vector3.zero;
    public ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding;

    private RenderTexture rt;
    private ulong overlayHandle = OpenVR.k_ulOverlayHandleInvalid;
    private bool initialized = false;

    void Start()
    {
        // OpenVR Overlay 初期化
        var err = EVRInitError.None;
        OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
        if (err != EVRInitError.None)
        {
            Debug.LogError("OpenVR init failed: " + err);
            return;
        }

        rt = new RenderTexture(1024, 1024, 0, RenderTextureFormat.ARGB32);
        rt.Create();

        // Overlay作成
        var ov = OpenVR.Overlay;
        var createErr = ov.CreateOverlay("ukemochi.overlay.camera", "Camera Overlay", ref overlayHandle);
        if (createErr != EVROverlayError.None)
        {
            Debug.LogError("CreateOverlay failed: " + createErr);
            return;
        }

        ov.SetOverlayWidthInMeters(overlayHandle, widthInMeters);
        ov.SetOverlayAlpha(overlayHandle, 1.0f);
        ov.SetOverlaySortOrder(overlayHandle, 400); // 前景優先

        // ★追加: DirectX/OpenGL のUV違いに対応して上下反転を修正
        ApplyTextureBoundsForAPI();

        SetTransformAbsoluteInWorld();
        ov.ShowOverlay(overlayHandle);
        initialized = true;
    }

    // ★追加メソッド：上下反転補正
    void ApplyTextureBoundsForAPI()
    {
        var bounds = new VRTextureBounds_t();

        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11
            || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12)
        {
            // DirectXではテクスチャが上下反転するためvを逆に
            bounds.uMin = 0f; bounds.vMin = 1f;
            bounds.uMax = 1f; bounds.vMax = 0f;
        }
        else
        {
            // OpenGL等は通常
            bounds.uMin = 0f; bounds.vMin = 0f;
            bounds.uMax = 1f; bounds.vMax = 1f;
        }

        OpenVR.Overlay.SetOverlayTextureBounds(overlayHandle, ref bounds);
    }

    void SetTransformAbsoluteInWorld()
    {
        // ワールド絶対座標での 3x4 行列（回転 + 平行移動）
        HmdMatrix34_t m = new HmdMatrix34_t();

        var q = Quaternion.Euler(worldEuler);
        var R = Matrix4x4.Rotate(q);

        // 回転成分
        m.m0 = R.m00; m.m1 = R.m01; m.m2 = R.m02;
        m.m4 = R.m10; m.m5 = R.m11; m.m6 = R.m12;
        m.m8 = R.m20; m.m9 = R.m21; m.m10 = R.m22;

        // 平行移動成分（メートル）
        m.m3 = worldPosition.x;
        m.m7 = worldPosition.y;
        m.m11 = worldPosition.z;

        OpenVR.Overlay.SetOverlayTransformAbsolute(overlayHandle, origin, ref m);
    }

    void Update()
    {
        if (!initialized || stream == null || chromaBlitMaterial == null) return;

        var src = stream.LatestTexture; // MJPEG の最新フレームを取得
        if (src == null) return;

        chromaBlitMaterial.SetColor("_ChromaColor", chromaColor);
        chromaBlitMaterial.SetFloat("_Threshold", threshold);

        // クロマキーを適用しつつRenderTextureへブリット
        Graphics.Blit(src, rt, chromaBlitMaterial);

        // RenderTexture を Overlay へ送信
        var tex = new Texture_t()
        {
            handle = rt.GetNativeTexturePtr(),
            eType = (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
                    ? ETextureType.DirectX
                    : ETextureType.OpenGL,
            eColorSpace = EColorSpace.Auto
        };
        OpenVR.Overlay.SetOverlayTexture(overlayHandle, ref tex);
    }

    void OnDestroy()
    {
        if (OpenVR.Overlay != null && overlayHandle != OpenVR.k_ulOverlayHandleInvalid)
        {
            OpenVR.Overlay.HideOverlay(overlayHandle);
            OpenVR.Overlay.DestroyOverlay(overlayHandle);
        }

        OpenVR.Shutdown();
        if (rt != null)
        {
            rt.Release();
        }
    }
}
