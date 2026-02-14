using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Valve.VR;

public class ViveModeCyclerSteamVR : MonoBehaviour
{
    [Header("SteamVR Boolean Actions (Inspectorで割り当て)")]
    public SteamVR_Action_Boolean gripAction;     // /actions/default/in/ModeGrip
    public SteamVR_Action_Boolean padClickAction; // /actions/default/in/ModePadClick

    [Header("Python Flask base URL")]
    public string baseUrl = "http://127.0.0.1:5000";

    public float chordCooldown = 0.25f;
    private float lastChordAt = -999f;
    private readonly string[] order = { "detect", "mask", "restore" };
    private int idx = -1;

    // ★ Awake/OnEnableで無理にInitializeしない
    IEnumerator Start()
    {
        // 1フレーム待ってからアクションセットを有効化（読み込み順の競合回避）
        yield return null;
        try
        {
            SteamVR_Actions._default.Activate(SteamVR_Input_Sources.Any, 0, true);
            Debug.Log("[ModeCycler] Activated action set: /actions/default");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ModeCycler] Activate failed (will try anyway): {e.Message}");
        }
    }

    void Update()
    {
        if (Time.time - lastChordAt < chordCooldown) return;

        bool gripHeld =
            (gripAction != null &&
             (gripAction.GetState(SteamVR_Input_Sources.LeftHand) ||
              gripAction.GetState(SteamVR_Input_Sources.RightHand)));

        bool padClicked =
            (padClickAction != null &&
             (padClickAction.GetStateDown(SteamVR_Input_Sources.LeftHand) ||
              padClickAction.GetStateDown(SteamVR_Input_Sources.RightHand)));

        // 入力が来ているかのログ
        if (gripHeld) Debug.Log("[ModeCycler] grip held");
        if (padClicked) Debug.Log("[ModeCycler] pad click down");

        if (gripHeld && padClicked)
        {
            lastChordAt = Time.time;
            StartCoroutine(SetNextMode());
        }
    }

    IEnumerator SetNextMode()
    {
        idx = (idx + 1) % order.Length;
        string mode = order[idx];
        string url = $"{baseUrl}/set_mode/{mode}";
        Debug.Log($"[ModeCycler] sending: {url}");
        using (var req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError($"[ModeCycler] SetMode {mode} failed: {req.error}");
            else
                Debug.Log($"[ModeCycler] Mode changed to: {mode}");
        }
    }
}
