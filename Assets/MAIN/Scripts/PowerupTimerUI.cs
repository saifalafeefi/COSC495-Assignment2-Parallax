using UnityEngine;
using TMPro;
using UnityEngine.UI;

// displays status bars for powerups
// duration-based: knockback, giant, haunt
// condition-based: smash (charges), shield (hits/stacks)
// attach to a UI panel — rows are created dynamically, no manual child setup needed
public class PowerupTimerUI : MonoBehaviour
{
    [SerializeField] private PlayerControllerX playerController;
    [SerializeField] private float labelFontSize = 20f;
    [SerializeField] private float timerFontSize = 18f;
    [SerializeField] private float rowHeight = 44f;
    [SerializeField] private float rowSpacing = 4f;
    [SerializeField] private float paddingLeft = 10f;
    [SerializeField] private float paddingTop = 10f;
    [SerializeField] private float barWidth = 220f;
    [SerializeField] private float barHeight = 14f;
    [SerializeField] private float barYOffset = 22f;
    [Header("Pulse (Conditional Powerups)")]
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseScale = 0.1f;
    [SerializeField] private float pulseMinAlpha = 0.55f;
    [SerializeField] private float pulseMaxAlpha = 1f;

    // colors match the powerup overlay tints
    private static readonly Color knockbackColor = new Color(1f, 0.85f, 0.2f);  // yellow
    private static readonly Color smashColor = new Color(1f, 0.25f, 0.25f);      // red
    private static readonly Color shieldColor = new Color(0.25f, 1f, 1f);        // cyan
    private static readonly Color giantColor = new Color(0.2f, 1f, 0.2f);       // green
    private static readonly Color hauntColor = new Color(0.7f, 0.2f, 1f);       // purple
    private static readonly Color barBackgroundColor = new Color(0f, 0f, 0f, 0.45f);

    private class PowerupRow
    {
        public RectTransform root;
        public TextMeshProUGUI timer;
        public RectTransform fillRect;
        public Image fillImage;
        public Color fillBaseColor;
    }

    private PowerupRow knockbackRow;
    private PowerupRow smashRow;
    private PowerupRow shieldRow;
    private PowerupRow giantRow;
    private PowerupRow hauntRow;

    private float knockbackDisplayMax;
    private float smashDisplayMaxStacks;
    private float shieldDisplayMaxUnits;
    private float giantDisplayMax;
    private float hauntDisplayMax;

    void Start()
    {
        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerControllerX>();

        knockbackRow = CreateRow("Knockback", knockbackColor);
        smashRow = CreateRow("Smash", smashColor);
        shieldRow = CreateRow("Shield", shieldColor);
        giantRow = CreateRow("Giant", giantColor);
        hauntRow = CreateRow("Haunt", hauntColor);
    }

    void Update()
    {
        if (playerController == null) return;

        UpdateTimedRow(knockbackRow, playerController.KnockbackTimer, ref knockbackDisplayMax);
        UpdateSmashRow();
        UpdateShieldRow();
        UpdateTimedRow(giantRow, playerController.GiantTimer, ref giantDisplayMax);
        UpdateTimedRow(hauntRow, playerController.HauntTimer, ref hauntDisplayMax);
        ReflowRows();
    }

    void UpdateTimedRow(PowerupRow row, float timerValue, ref float displayMax)
    {
        if (row == null || row.root == null) return;

        if (timerValue > 0f)
        {
            if (timerValue > displayMax)
                displayMax = timerValue;

            row.root.gameObject.SetActive(true);
            float ratio = Mathf.Clamp01(timerValue / Mathf.Max(0.01f, displayMax));
            if (row.fillRect != null)
                row.fillRect.sizeDelta = new Vector2(barWidth * ratio, barHeight);
            row.timer.text = $"{timerValue:F1}s";
            ApplyPulse(row, false);
        }
        else
        {
            displayMax = 0f;
            row.root.gameObject.SetActive(false);
            ApplyPulse(row, false);
        }
    }

    void UpdateSmashRow()
    {
        if (smashRow == null || smashRow.root == null) return;

        int stacks = playerController.SmashStacks;
        if (stacks > 0)
        {
            if (stacks > smashDisplayMaxStacks)
                smashDisplayMaxStacks = stacks;

            float ratio = Mathf.Clamp01(stacks / Mathf.Max(1f, smashDisplayMaxStacks));
            smashRow.root.gameObject.SetActive(true);
            if (smashRow.fillRect != null)
                smashRow.fillRect.sizeDelta = new Vector2(barWidth * ratio, barHeight);
            smashRow.timer.text = "x" + stacks;
            ApplyPulse(smashRow, true);
        }
        else
        {
            smashDisplayMaxStacks = 0f;
            smashRow.root.gameObject.SetActive(false);
            ApplyPulse(smashRow, false);
        }
    }

    void UpdateShieldRow()
    {
        if (shieldRow == null || shieldRow.root == null) return;

        int stacks = playerController.ShieldStacks;
        int maxHits = Mathf.Max(1, playerController.ShieldMaxHits);
        int hitsRemaining = Mathf.Clamp(playerController.ShieldHitsRemaining, 0, maxHits);

        if (stacks > 0)
        {
            float totalUnits = ((stacks - 1) * maxHits) + hitsRemaining;
            if (totalUnits > shieldDisplayMaxUnits)
                shieldDisplayMaxUnits = totalUnits;

            float ratio = Mathf.Clamp01(totalUnits / Mathf.Max(1f, shieldDisplayMaxUnits));
            shieldRow.root.gameObject.SetActive(true);
            if (shieldRow.fillRect != null)
                shieldRow.fillRect.sizeDelta = new Vector2(barWidth * ratio, barHeight);
            shieldRow.timer.text = $"{hitsRemaining}/{maxHits} x{stacks}";
            ApplyPulse(shieldRow, true);
        }
        else
        {
            shieldDisplayMaxUnits = 0f;
            shieldRow.root.gameObject.SetActive(false);
            ApplyPulse(shieldRow, false);
        }
    }

    void ApplyPulse(PowerupRow row, bool enabled)
    {
        if (row == null) return;

        if (!enabled)
        {
            if (row.fillRect != null) row.fillRect.localScale = Vector3.one;
            if (row.fillImage != null) row.fillImage.color = row.fillBaseColor;
            return;
        }

        float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float yScale = Mathf.Lerp(1f, 1f + pulseScale, wave);
        float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, wave);

        if (row.fillRect != null)
            row.fillRect.localScale = new Vector3(1f, yScale, 1f);

        if (row.fillImage != null)
        {
            Color c = row.fillBaseColor;
            c.a = alpha;
            row.fillImage.color = c;
        }
    }

    void ReflowRows()
    {
        PowerupRow[] rows = { knockbackRow, smashRow, shieldRow, giantRow, hauntRow };
        int visibleIndex = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] != null && rows[i].root != null && rows[i].root.gameObject.activeSelf)
            {
                RectTransform rt = rows[i].root;
                rt.anchoredPosition = new Vector2(paddingLeft, -(paddingTop + visibleIndex * (rowHeight + rowSpacing)));
                visibleIndex++;
            }
        }
    }

    PowerupRow CreateRow(string labelText, Color color)
    {
        GameObject row = new GameObject(labelText + " Row", typeof(RectTransform));
        row.transform.SetParent(transform, false);
        RectTransform rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0, 1);
        rowRt.anchorMax = new Vector2(0, 1);
        rowRt.pivot = new Vector2(0, 1);
        rowRt.sizeDelta = new Vector2(barWidth + 120f, rowHeight);

        GameObject titleObj = new GameObject("Title", typeof(RectTransform));
        titleObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = labelText;
        title.fontSize = labelFontSize;
        title.color = color;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Left;
        title.enableWordWrapping = false;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0, 1);
        titleRt.anchorMax = new Vector2(0, 1);
        titleRt.pivot = new Vector2(0, 1);
        titleRt.anchoredPosition = Vector2.zero;
        titleRt.sizeDelta = new Vector2(barWidth, 20f);

        GameObject timerObj = new GameObject("Timer", typeof(RectTransform));
        timerObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI timer = timerObj.AddComponent<TextMeshProUGUI>();
        timer.fontSize = timerFontSize;
        timer.color = Color.white;
        timer.fontStyle = FontStyles.Bold;
        timer.alignment = TextAlignmentOptions.Right;
        timer.enableWordWrapping = false;
        RectTransform timerRt = timer.rectTransform;
        timerRt.anchorMin = new Vector2(0, 1);
        timerRt.anchorMax = new Vector2(0, 1);
        timerRt.pivot = new Vector2(0, 1);
        timerRt.anchoredPosition = new Vector2(barWidth + 8f, 0f);
        timerRt.sizeDelta = new Vector2(90f, 20f);

        GameObject bgObj = new GameObject("Bar BG", typeof(RectTransform));
        bgObj.transform.SetParent(row.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = barBackgroundColor;
        RectTransform bgRt = bg.rectTransform;
        bgRt.anchorMin = new Vector2(0, 1);
        bgRt.anchorMax = new Vector2(0, 1);
        bgRt.pivot = new Vector2(0, 1);
        bgRt.anchoredPosition = new Vector2(0f, -barYOffset);
        bgRt.sizeDelta = new Vector2(barWidth, barHeight);

        GameObject fillObj = new GameObject("Bar Fill", typeof(RectTransform));
        fillObj.transform.SetParent(bgObj.transform, false);
        Image fill = fillObj.AddComponent<Image>();
        fill.color = color;
        RectTransform fillRt = fill.rectTransform;
        fillRt.anchorMin = new Vector2(0, 0);
        fillRt.anchorMax = new Vector2(0, 1);
        fillRt.pivot = new Vector2(0, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = new Vector2(barWidth, barHeight);

        row.SetActive(false);
        return new PowerupRow
        {
            root = rowRt,
            timer = timer,
            fillRect = fillRt,
            fillImage = fill,
            fillBaseColor = color
        };
    }
}
