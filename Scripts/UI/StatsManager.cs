using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Stats画面マネージャー（ローカルデータ版）
/// StudyManagerのローカルデータを参照して表示。
/// </summary>
public class StatsManager : MonoBehaviour
{
    [Header("Dependency")]
    [SerializeField] private StudyManager studyManager;

    [Header("Master Rate Card")]
    [SerializeField] private TextMeshProUGUI rateText;
    [SerializeField] private Slider rateGauge;

    [Header("Streak Card")]
    [SerializeField] private TextMeshProUGUI streakText;

    [Header("Level Card")]
    [SerializeField] private Slider lv1Bar;
    [SerializeField] private Slider lv2Bar;
    [SerializeField] private Slider lv3Bar;
    [SerializeField] private TextMeshProUGUI lv1Text;
    [SerializeField] private TextMeshProUGUI lv2Text;
    [SerializeField] private TextMeshProUGUI lv3Text;

    [Header("Week Graph")]
    [SerializeField] private RectTransform graphArea;
    [SerializeField] private GameObject barPrefab;
    [SerializeField] private Color barColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color emptyBarColor = new Color(0.85f, 0.85f, 0.85f);

    private void OnEnable()
    {
        if (studyManager != null)
            Refresh();
    }

    public void Refresh()
    {
        UpdateMasterRate();
        UpdateStreak();
        UpdateLevelProgress();
        UpdateWeekGraph();
    }

    private void UpdateMasterRate()
    {
        int mastered = studyManager.GetMasteredCount();
        int total    = studyManager.GetTotalCount();
        float rate = total > 0 ? (float)mastered / total : 0f;

        if (rateText != null)
            rateText.text = $"{Mathf.RoundToInt(rate * 100)}%";

        if (rateGauge != null)
        {
            rateGauge.minValue = 0f;
            rateGauge.maxValue = 1f;
            rateGauge.value = rate;
            rateGauge.interactable = false;
        }
    }

    private void UpdateStreak()
    {
        var logs = studyManager.GetReviewLogs();
        if (logs == null || logs.Count == 0)
        {
            if (streakText != null) streakText.text = "0日";
            return;
        }

        var studyDates = new HashSet<DateTime>();
        foreach (var log in logs)
        {
            DateTime d = new DateTime(log.createdTicks).Date;
            studyDates.Add(d);
        }

        int streak = 0;
        DateTime today = DateTime.Today;
        DateTime cursor = studyDates.Contains(today) ? today : today.AddDays(-1);

        while (studyDates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        if (streakText != null)
            streakText.text = $"{streak}日";
    }

    private void UpdateLevelProgress()
    {
        var allQuestions = studyManager.GetAllQuestions();
        var userQuestions = studyManager.GetUserQuestions();

        var totals   = new Dictionary<int, int> { {1, 0}, {2, 0}, {3, 0} };
        var mastered = new Dictionary<int, int> { {1, 0}, {2, 0}, {3, 0} };

        foreach (var q in allQuestions)
        {
            if (!totals.ContainsKey(q.level)) continue;
            totals[q.level]++;

            if (userQuestions.TryGetValue(q.id, out var uq))
            {
                if (uq.strength >= 0.8f && uq.knownCount >= 2)
                    mastered[q.level]++;
            }
        }

        SetLevelBar(lv1Bar, lv1Text, mastered[1], totals[1]);
        SetLevelBar(lv2Bar, lv2Text, mastered[2], totals[2]);
        SetLevelBar(lv3Bar, lv3Text, mastered[3], totals[3]);
    }

    private void SetLevelBar(Slider bar, TextMeshProUGUI text, int mastered, int total)
    {
        if (bar != null)
        {
            bar.minValue = 0f;
            bar.maxValue = 1f;
            bar.value = total > 0 ? (float)mastered / total : 0f;
            bar.interactable = false;
        }
        if (text != null)
            text.text = $"{mastered} / {total}";
    }

    private void UpdateWeekGraph()
    {
        if (graphArea == null || barPrefab == null) return;

        for (int c = graphArea.childCount - 1; c >= 0; c--)
            DestroyImmediate(graphArea.GetChild(c).gameObject);

        var logs = studyManager.GetReviewLogs();

        var dayStats = new Dictionary<DateTime, (int total, int correct)>();
        if (logs != null)
        {
            foreach (var log in logs)
            {
                DateTime d = new DateTime(log.createdTicks).Date;
                if (!dayStats.ContainsKey(d))
                    dayStats[d] = (0, 0);

                var cur = dayStats[d];
                dayStats[d] = (cur.total + 1, cur.correct + (log.isKnown ? 1 : 0));
            }
        }

        float areaWidth  = graphArea.rect.width;
        float areaHeight = graphArea.rect.height;

        int barCount = 7;
        float spacing = 6f;
        float barWidth = (areaWidth - spacing * (barCount - 1)) / barCount;
        float minBarHeight = areaHeight * 0.08f;

        DateTime today = DateTime.Today;

        for (int i = 0; i < barCount; i++)
        {
            DateTime day = today.AddDays(-(barCount - 1 - i));

            int total = 0;
            int correct = 0;
            if (dayStats.TryGetValue(day, out var stat))
            {
                total = stat.total;
                correct = stat.correct;
            }

            bool hasData = total > 0;
            float rate = hasData ? (float)correct / total : 0f;

            float barHeight = hasData
                ? Mathf.Max(areaHeight * rate, minBarHeight)
                : minBarHeight;

            GameObject bar = Instantiate(barPrefab, graphArea);
            var rt = bar.GetComponent<RectTransform>();

            if (rt != null)
            {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.sizeDelta = new Vector2(barWidth, barHeight);
                float x = i * (barWidth + spacing);
                rt.anchoredPosition = new Vector2(x, 0);
            }

            var img = bar.GetComponent<Image>();
            if (img != null)
                img.color = hasData ? barColor : emptyBarColor;

            var label = bar.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string dayName = day.ToString("M/d");
                label.text = hasData
                    ? $"{dayName}\n{Mathf.RoundToInt(rate * 100)}%"
                    : $"{dayName}";

                var labelRt = label.GetComponent<RectTransform>();
                if (labelRt != null)
                {
                    labelRt.anchorMin = new Vector2(0.5f, 0);
                    labelRt.anchorMax = new Vector2(0.5f, 0);
                    labelRt.pivot = new Vector2(0.5f, 1f);
                    labelRt.anchoredPosition = new Vector2(0, -4f);
                }
            }
        }
    }
}
