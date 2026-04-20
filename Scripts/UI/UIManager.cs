using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 画面切替管理
/// QuesPanel / StatsPanel はタブ切替
/// Daily / Settings は別シーンへ遷移
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject quesPanel;
    [SerializeField] private GameObject statsPanel;

    [Header("Tab Buttons")]
    [SerializeField] private Button btnQues;
    [SerializeField] private Button btnStats;
    [SerializeField] private Button btnDaily;

    [Header("Settings Button")]
    [SerializeField] private Button btnSettings;

    [Header("Scene Names")]
    [SerializeField] private string dailySceneName    = "DailyScene";
    [SerializeField] private string settingsSceneName = "SettingsScene";

    private void Start()
    {
        btnQues.onClick.AddListener(OpenQues);
        btnStats.onClick.AddListener(OpenStats);
        btnDaily.onClick.AddListener(OpenDaily);

        if (btnSettings != null)
            btnSettings.onClick.AddListener(OpenSettings);

        OpenQues();
    }

    public void OpenQues()
    {
        quesPanel.SetActive(true);
        statsPanel.SetActive(false);
    }

    [Header("Managers")]
    [SerializeField] private StatsManager statsManager;

    public void OpenStats()
    {
        quesPanel.SetActive(false);
        statsPanel.SetActive(true);

        // 直接Refreshを呼ぶ
        if (statsManager != null)
            statsManager.Refresh();
    }

    public void OpenDaily()
    {
        SceneManager.LoadScene(dailySceneName);
    }

    public void OpenSettings()
    {
        SceneManager.LoadScene(settingsSceneName);
    }
}
