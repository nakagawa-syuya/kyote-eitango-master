using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Daily（カレンダー）画面マネージャー（ローカルデータ版）
/// PlayerPrefsのreview_logsを参照して表示。
/// </summary>
public class DailyManager : MonoBehaviour
{
    [Header("Cell Colors")]
    [SerializeField] private Color colorStudied  = new Color(0.35f, 0.65f, 1.0f);
    [SerializeField] private Color colorToday    = new Color(1.0f, 0.75f, 0.25f);
    [SerializeField] private Color colorEmpty    = new Color(0.95f, 0.95f, 0.95f);
    [SerializeField] private Color colorOutside  = new Color(0.85f, 0.85f, 0.85f);

    [Header("Scene Names")]
    [SerializeField] private string questSceneName = "Ques";
    [SerializeField] private string statsSceneName = "Ques";

    [Header("Navigation Buttons (Optional)")]
    [SerializeField] private Button btnPrevMonth;
    [SerializeField] private Button btnNextMonth;
    [SerializeField] private Button btnToday;
    [SerializeField] private Button btnQues;
    [SerializeField] private Button btnStats;

    private TextMeshProUGUI _textTitle;
    private Image[] _imageCells    = new Image[42];
    private TextMeshProUGUI[] _textDates = new TextMeshProUGUI[42];
    private TextMeshProUGUI[] _textRecs  = new TextMeshProUGUI[42];

    private int displayYear;
    private int displayMonth;

    private Dictionary<string, DayStats> dayStatsMap = new Dictionary<string, DayStats>();

    private class DayStats
    {
        public int total;
        public int correct;
    }

    [Serializable]
    private class SaveData
    {
        public List<StudyManager.UserQuestion> uq;
        public List<StudyManager.ReviewLog> logs;
        public string refresh;
    }

    private void Start()
    {
        FindUIObjects();
        LoadReviewLogs();

        if (btnPrevMonth != null) btnPrevMonth.onClick.AddListener(OnPrevMonth);
        if (btnNextMonth != null) btnNextMonth.onClick.AddListener(OnNextMonth);
        if (btnToday != null)     btnToday.onClick.AddListener(OnToday);
        if (btnQues != null)  btnQues.onClick.AddListener(()  => SceneManager.LoadScene(questSceneName));
        if (btnStats != null) btnStats.onClick.AddListener(() => SceneManager.LoadScene(statsSceneName));

        DateTime today = DateTime.Today;
        displayYear  = today.Year;
        displayMonth = today.Month;

        RefreshCalendar();
    }

    private void FindUIObjects()
    {
        var titleObj = GameObject.Find("Text_Title");
        if (titleObj != null)
            _textTitle = titleObj.GetComponent<TextMeshProUGUI>();

        for (int i = 0; i < 42; i++)
        {
            string idx = (i + 1).ToString("00");

            var imgObj  = GameObject.Find("Image_" + idx);
            var dateObj = GameObject.Find("Text_date_" + idx);
            var recObj  = GameObject.Find("Text_rec_" + idx);

            if (imgObj  != null) _imageCells[i] = imgObj.GetComponent<Image>();
            if (dateObj != null) _textDates[i]  = dateObj.GetComponent<TextMeshProUGUI>();
            if (recObj  != null) _textRecs[i]   = recObj.GetComponent<TextMeshProUGUI>();
        }
    }

    private void LoadReviewLogs()
    {
        dayStatsMap.Clear();

        string json = PlayerPrefs.GetString("VocabSave", "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.logs == null) return;

            foreach (var log in data.logs)
            {
                DateTime d = new DateTime(log.createdTicks).Date;
                string key = d.ToString("yyyy-MM-dd");

                if (!dayStatsMap.ContainsKey(key))
                    dayStatsMap[key] = new DayStats();

                dayStatsMap[key].total++;
                if (log.isKnown) dayStatsMap[key].correct++;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DailyManager] ログ読込エラー: {e.Message}");
        }
    }

    private void RefreshCalendar()
    {
        if (_textTitle != null)
            _textTitle.text = $"{displayYear}年 {displayMonth}月";

        DateTime firstDay = new DateTime(displayYear, displayMonth, 1);
        int firstDayOfWeek = (int)firstDay.DayOfWeek;

        DateTime today = DateTime.Today;
        string todayKey = today.ToString("yyyy-MM-dd");

        for (int i = 0; i < 42; i++)
        {
            int dayOffset = i - firstDayOfWeek;
            DateTime cellDate = firstDay.AddDays(dayOffset);

            bool isInMonth = (cellDate.Year == displayYear && cellDate.Month == displayMonth);
            string key = cellDate.ToString("yyyy-MM-dd");

            if (_textDates[i] != null)
            {
                _textDates[i].text = cellDate.Day.ToString();

                if (!isInMonth)
                    _textDates[i].color = new Color(0.6f, 0.6f, 0.6f);
                else if (key == todayKey)
                    _textDates[i].color = Color.white;
                else if (dayStatsMap.ContainsKey(key))
                    _textDates[i].color = Color.white;
                else
                    _textDates[i].color = new Color(0.2f, 0.2f, 0.2f);
            }

            if (_textRecs[i] != null)
            {
                if (isInMonth && dayStatsMap.TryGetValue(key, out var stat))
                {
                    int rate = Mathf.RoundToInt((float)stat.correct / stat.total * 100f);
                    _textRecs[i].text = $"{stat.total}問\n{rate}%";
                }
                else
                {
                    _textRecs[i].text = "";
                }
            }

            if (_imageCells[i] != null)
            {
                if (!isInMonth)
                    _imageCells[i].color = colorOutside;
                else if (key == todayKey)
                    _imageCells[i].color = colorToday;
                else if (dayStatsMap.ContainsKey(key))
                    _imageCells[i].color = colorStudied;
                else
                    _imageCells[i].color = colorEmpty;
            }
        }
    }

    public void OnPrevMonth()
    {
        displayMonth--;
        if (displayMonth < 1)
        {
            displayMonth = 12;
            displayYear--;
        }
        RefreshCalendar();
    }

    public void OnNextMonth()
    {
        displayMonth++;
        if (displayMonth > 12)
        {
            displayMonth = 1;
            displayYear++;
        }
        RefreshCalendar();
    }

    public void OnToday()
    {
        DateTime today = DateTime.Today;
        displayYear  = today.Year;
        displayMonth = today.Month;
        RefreshCalendar();
    }
}
