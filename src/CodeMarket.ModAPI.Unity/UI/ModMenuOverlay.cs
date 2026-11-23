using System.Collections.Generic;
using CodeMarket.ModAPI.Core;
using CodeMarket.ModAPI.Core.Lifecycle;
using UnityEngine;

namespace CodeMarket.ModAPI.Unity.UI;

internal sealed class ModMenuOverlay : MonoBehaviour
{
    private const int WindowId = 48291;
    private Rect _windowRect = new Rect(40f, 60f, 600f, 460f);
    private Vector2 _scrollPos;
    private bool _bannerDismissed;
    private readonly List<string> _sidecarWarnings = new List<string>();

    private Texture2D? _windowBg;
    private Texture2D? _cardBg;
    private Texture2D? _buttonBg;
    private Texture2D? _buttonHoverBg;
    private Texture2D? _bannerBg;

    private GUIStyle? _windowStyle;
    private GUIStyle? _cardStyle;
    private GUIStyle? _buttonStyle;
    private GUIStyle? _headerStyle;
    private GUIStyle? _bodyStyle;
    private GUIStyle? _mutedStyle;
    private GUIStyle? _activeStyle;
    private GUIStyle? _disabledStyle;
    private GUIStyle? _quarantineLabelStyle;
    private GUIStyle? _warnBannerStyle;
    private string? _statusFeedback;

    public bool IsVisible { get; private set; }

    public void NotifySidecarWarning(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning)) return;
        _sidecarWarnings.Add(warning);
        _bannerDismissed = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(ModApiRuntime.ModMenuToggleKey))
        {
            IsVisible = !IsVisible;
        }
    }

    private void OnDestroy()
    {
        DestroyTexture(ref _windowBg);
        DestroyTexture(ref _cardBg);
        DestroyTexture(ref _buttonBg);
        DestroyTexture(ref _buttonHoverBg);
        DestroyTexture(ref _bannerBg);
    }

    private void OnGUI()
    {
        int quarantinedCount = ModApiRuntime.Lifecycle.QuarantinedMods.Count;
        bool hasWarnings = quarantinedCount > 0 || _sidecarWarnings.Count > 0;

        if (!IsVisible && (!hasWarnings || _bannerDismissed))
        {
            return;
        }

        EnsureStyles();

        if (hasWarnings && !_bannerDismissed)
        {
            DrawWarningBanner(quarantinedCount);
        }

        if (IsVisible)
        {
            _windowRect = GUI.Window(
                WindowId,
                _windowRect,
                DrawModMenuWindow,
                $"{ModApiInfo.PluginName} v{ModApiInfo.PluginVersion}  (Toggle: {ModApiRuntime.ModMenuToggleKey})",
                _windowStyle);
        }
    }

    private void DrawWarningBanner(int quarantinedCount)
    {
        float width = Mathf.Min(Screen.width - 40f, 760f);
        var rect = new Rect((Screen.width - width) * 0.5f, 12f, width, 38f);

        GUILayout.BeginArea(rect, _warnBannerStyle);
        GUILayout.BeginHorizontal();

        string msg = quarantinedCount > 0
            ? $"[CodeMarket.ModAPI] {quarantinedCount} mod(s) quarantined due to errors."
            : "[CodeMarket.ModAPI] Sidecar save warning detected.";

        GUILayout.Label(msg, _bodyStyle);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button($"Details ({ModApiRuntime.ModMenuToggleKey})", _buttonStyle, GUILayout.Width(115f)))
        {
            IsVisible = true;
        }

        if (GUILayout.Button("Dismiss", _buttonStyle, GUILayout.Width(76f)))
        {
            _bannerDismissed = true;
        }

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void DrawModMenuWindow(int id)
    {
        GUILayout.BeginVertical(_cardStyle);
        GUILayout.Label("Safety & Runtime Status", _headerStyle);
        GUILayout.Label(
            $"Steam Leaderboards: {(ModApiRuntime.LeaderboardGuardActive ? "DISABLED (NoDayBoard active)" : "Active")}",
            _bodyStyle);
        GUILayout.Label(
            $"Steam Achievements: {(ModApiRuntime.DisableAchievementsWhenModded ? "Suppressed (config)" : "Enabled (default)")}",
            _bodyStyle);
        GUILayout.EndVertical();

        GUILayout.Space(8f);

        IReadOnlyList<ModRecord> mods = ModApiRuntime.Lifecycle.Mods;
        GUILayout.Label($"Discovered Mods ({mods.Count})", _headerStyle);
        GUILayout.Space(4f);

        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

        if (mods.Count == 0)
        {
            GUILayout.Label("No external plugins or data packs loaded.", _mutedStyle);
        }
        else
        {
            foreach (ModRecord mod in mods)
            {
                GUILayout.BeginVertical(_cardStyle);
                string title = mod.Manifest is not null
                    ? $"{mod.Manifest.Name} ({mod.Id}) v{mod.Manifest.Version} [{mod.Manifest.Type}]"
                    : mod.Id;

                GUIStyle stateStyle = mod.State switch
                {
                    ModState.Quarantined => _quarantineLabelStyle!,
                    ModState.Active => _activeStyle!,
                    ModState.Disabled => _disabledStyle!,
                    _ => _bodyStyle!
                };

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{title} — {mod.State}", stateStyle);
                GUILayout.FlexibleSpace();

                if (mod.State != ModState.Quarantined)
                {
                    bool isDisabled = mod.State == ModState.Disabled;
                    string btnLabel = isDisabled ? "Enable" : "Disable";
                    if (GUILayout.Button(btnLabel, _buttonStyle, GUILayout.Width(76f), GUILayout.Height(24f)))
                    {
                        bool ok = ModApiRuntime.SetModEnabled(mod.Id, enabled: isDisabled);
                        _statusFeedback = ok
                            ? $"{(isDisabled ? "Enabled" : "Disabled")} '{mod.Id}' (saved to CodeMarket.ModAPI.cfg)."
                            : $"Failed to toggle '{mod.Id}' — check LogOutput.log.";
                    }
                }

                GUILayout.EndHorizontal();

                if (mod.Manifest is not null && !string.IsNullOrWhiteSpace(mod.Manifest.Description))
                {
                    GUILayout.Label(mod.Manifest.Description, _mutedStyle);
                }

                if (mod.State == ModState.Quarantined && !string.IsNullOrWhiteSpace(mod.QuarantineReason))
                {
                    GUILayout.Label($"Phase: {mod.QuarantinePhase} | Reason: {mod.QuarantineReason}", _quarantineLabelStyle);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        if (_sidecarWarnings.Count > 0)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Sidecar Warnings", _headerStyle);
            foreach (string warn in _sidecarWarnings)
            {
                GUILayout.Label(warn, _quarantineLabelStyle);
            }
        }

        GUILayout.EndScrollView();

        if (!string.IsNullOrWhiteSpace(_statusFeedback))
        {
            GUILayout.Space(4f);
            GUILayout.Label(_statusFeedback, _mutedStyle);
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Close", _buttonStyle, GUILayout.Height(30f)))
        {
            IsVisible = false;
        }

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
    }

    private void EnsureStyles()
    {
        if (_windowStyle is not null) return;

        // Opaque dark slate textures prevent bright 3D store backgrounds from bleeding through IMGUI controls.
        _windowBg = MakeSolidTexture(new Color(0.08f, 0.10f, 0.14f, 0.96f));
        _cardBg = MakeSolidTexture(new Color(0.13f, 0.16f, 0.22f, 0.98f));
        _buttonBg = MakeSolidTexture(new Color(0.18f, 0.23f, 0.32f, 1f));
        _buttonHoverBg = MakeSolidTexture(new Color(0.25f, 0.32f, 0.44f, 1f));
        _bannerBg = MakeSolidTexture(new Color(0.32f, 0.11f, 0.11f, 0.97f));

        Color textPrimary = new Color(0.95f, 0.96f, 0.99f, 1f);
        Color textMuted = new Color(0.76f, 0.80f, 0.88f, 1f);
        Color textAccent = new Color(0.45f, 0.75f, 1f, 1f);
        Color textActive = new Color(0.49f, 0.91f, 0.53f, 1f);
        Color textDisabled = new Color(0.96f, 0.78f, 0.36f, 1f);
        Color textError = new Color(1f, 0.48f, 0.45f, 1f);

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(14, 14, 30, 14)
        };
        _windowStyle.normal.background = _windowBg;
        _windowStyle.onNormal.background = _windowBg;
        _windowStyle.normal.textColor = textPrimary;
        _windowStyle.onNormal.textColor = textPrimary;

        _cardStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };
        _cardStyle.normal.background = _cardBg;
        _cardStyle.normal.textColor = textPrimary;

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(10, 10, 5, 5)
        };
        _buttonStyle.normal.background = _buttonBg;
        _buttonStyle.normal.textColor = textPrimary;
        _buttonStyle.hover.background = _buttonHoverBg;
        _buttonStyle.hover.textColor = Color.white;
        _buttonStyle.active.background = _buttonHoverBg;
        _buttonStyle.active.textColor = Color.white;

        _headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        _headerStyle.normal.textColor = textAccent;

        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true
        };
        _bodyStyle.normal.textColor = textPrimary;

        _mutedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true
        };
        _mutedStyle.normal.textColor = textMuted;

        _activeStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        _activeStyle.normal.textColor = textActive;

        _disabledStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        _disabledStyle.normal.textColor = textDisabled;

        _quarantineLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        _quarantineLabelStyle.normal.textColor = textError;

        _warnBannerStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(12, 12, 6, 6)
        };
        _warnBannerStyle.normal.background = _bannerBg;
        _warnBannerStyle.normal.textColor = textPrimary;
    }

    private static Texture2D MakeSolidTexture(Color color)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private static void DestroyTexture(ref Texture2D? tex)
    {
        if (tex != null)
        {
            Destroy(tex);
            tex = null;
        }
    }
}
