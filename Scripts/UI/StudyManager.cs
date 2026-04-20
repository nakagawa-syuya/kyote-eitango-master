using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 英単語学習アプリ - StudyManager
/// CsvLoaderから問題を受け取り、データ保存はAPI経由でMySQLへ。
/// オフライン時はローカルのみで動作可能。
/// </summary>
public class StudyManager : MonoBehaviour
{
    // ═══════════════════════════════════════════
    //  Inspector参照
    // ═══════════════════════════════════════════

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Slider progressBar;

    [Header("Question Card")]
    [SerializeField] private TextMeshProUGUI noText;
    [SerializeField] private TextMeshProUGUI wordText;
    [SerializeField] private Button showAnswerButton;

    [Header("Answer Panel")]
    [SerializeField] private GameObject answerPanel;
    [SerializeField] private TextMeshProUGUI meaningText;
    [SerializeField] private TextMeshProUGUI posText;

    [Header("Bottom Buttons")]
    [SerializeField] private Button knownButton;
    [SerializeField] private Button unknownButton;

    // ═══════════════════════════════════════════
    //  データクラス
    // ═══════════════════════════════════════════

    [Serializable]
    public class Question
    {
        public int id;
        public string title;
        public string answer;
        public string partOfSpeech;
        public int level;
    }

    [Serializable]
    public class UserQuestion
    {
        public int questionId;
        public float strength;
        public float kValue;
        public int reviewCount;
        public int knownCount;
        public int unknownCount;
        public long lastReviewTicks;
        public long nextReviewTicks;
        public long createdTicks;
    }

    [Serializable]
    public class ReviewLog
    {
        public int questionId;
        public bool isKnown;
        public float beforeStrength;
        public float afterStrength;
        public float beforeK;
        public float afterK;
        public long createdTicks;
    }

    // ═══════════════════════════════════════════
    //  定数
    // ═══════════════════════════════════════════

    private const float STR_MIN   = 0.1f;
    private const float STR_MAX   = 1.0f;
    private const float STR_DELTA = 0.20f;
    private const float K_INIT    = 0.05f;
    private const float K_MIN     = 0.01f;
    private const float K_MAX     = 0.20f;
    private const float K_KNOWN   = 0.95f;
    private const float K_UNKNOWN = 1.10f;
    private const float FIRST_K_S = 0.8f;
    private const float FIRST_U_S = 0.3f;
    private const float NR_MIN_H  = 1f;
    private const float NR_MAX_H  = 720f;
    private const float REV_RATIO = 0.7f;

    // ═══════════════════════════════════════════
    //  内部状態
    // ═══════════════════════════════════════════

    private List<Question> allQuestions = new List<Question>();
    private Dictionary<int, UserQuestion> userQuestions = new Dictionary<int, UserQuestion>();
    private List<ReviewLog> reviewLogs = new List<ReviewLog>();
    private Question currentQuestion;
    private string lastDailyRefresh;
    private ApiManager api;
    private bool useApi;
    private bool isProcessing; // 連打防止フラグ

    // ═══════════════════════════════════════════
    //  初期化
    // ═══════════════════════════════════════════

    private void Start()
    {
        showAnswerButton.onClick.AddListener(OnShowAnswer);
        knownButton.onClick.AddListener(() => ProcessAnswer(true));
        unknownButton.onClick.AddListener(() => ProcessAnswer(false));

        progressBar.minValue = 0f;
        progressBar.maxValue = 1f;
        progressBar.interactable = false;

        HideAnswer();

        // ApiManagerがあればAPI使用、なければローカルのみ
        api = ApiManager.Instance;
        useApi = (api != null);
    }

    /// <summary>CsvLoaderから呼び出す</summary>
    public void Initialize(List<Question> questions)
    {
        // API参照を再取得（シーン再読込時にも対応）
        api = ApiManager.Instance;
        useApi = (api != null);

        allQuestions = questions.OrderBy(q => q.id).ToList();
        Debug.Log($"[StudyManager] {allQuestions.Count}問 読み込み完了");

        LoadUserData();

        if (useApi)
        {
            // PlayerPrefsにuserIdがあればそのまま使う（Registerスキップ）
            if (UserManager.HasUserId())
            {
                api.SetUserId(UserManager.GetUserId());
                Debug.Log($"[StudyManager] userId復元: {api.GetUserId()}");
                StartStudy();
            }
            else
            {
                // 新規ユーザーのみRegister
                api.Register(success =>
                {
                    if (!success)
                        Debug.LogWarning("[StudyManager] ユーザー登録失敗（オフラインで続行）");

                    StartStudy();
                });
            }
        }
        else
        {
            StartStudy();
        }
    }

    private void StartStudy()
    {
        DailyRefresh();
        ShowNext();
    }

    // ═══════════════════════════════════════════
    //  起動時一括更新（§8）
    // ═══════════════════════════════════════════

    private void DailyRefresh()
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (lastDailyRefresh == today) return;

        DateTime now = DateTime.Now;

        foreach (var uq in userQuestions.Values)
        {
            if (uq.lastReviewTicks == 0) continue;

            double hours = (now - new DateTime(uq.lastReviewTicks)).TotalSeconds / 3600.0;
            if (hours <= 0) continue;

            float decayed = uq.strength * Mathf.Exp((float)(-uq.kValue * hours));
            uq.strength = Mathf.Clamp(decayed, STR_MIN, STR_MAX);
        }

        lastDailyRefresh = today;
        SaveUserData();

        // API側も一括更新
        if (useApi)
        {
            api.DailyRefresh(success =>
            {
                if (!success) Debug.LogWarning("[StudyManager] API一括更新失敗");
            });
        }
    }

    // ═══════════════════════════════════════════
    //  出題ロジック（§9）
    // ═══════════════════════════════════════════

    private void ShowNext()
    {
        HideAnswer();

        Question next;

        if (GetReviewCount() > 0)
        {
            if (UnityEngine.Random.value < REV_RATIO)
                next = PickReview();
            else
                next = PickNew() ?? PickReview();
        }
        else
        {
            next = PickNew() ?? PickWeak();
        }

        currentQuestion = next;

        if (currentQuestion != null)
            DisplayQuestion();
    }

    private int GetReviewCount()
    {
        long now = DateTime.Now.Ticks;
        return userQuestions.Values.Count(uq => uq.nextReviewTicks <= now);
    }

    private Question PickReview()
    {
        long now = DateTime.Now.Ticks;
        var uq = userQuestions.Values
            .Where(u => u.nextReviewTicks <= now)
            .OrderBy(u => u.nextReviewTicks)
            .FirstOrDefault();
        if (uq == null) return null;
        return allQuestions.FirstOrDefault(q => q.id == uq.questionId);
    }

    private Question PickNew()
    {
        foreach (var q in allQuestions)
            if (!userQuestions.ContainsKey(q.id)) return q;
        return null;
    }

    private Question PickWeak()
    {
        var uq = userQuestions.Values
            .OrderByDescending(u => u.kValue)
            .FirstOrDefault();
        if (uq == null) return null;
        return allQuestions.FirstOrDefault(q => q.id == uq.questionId);
    }

    // ═══════════════════════════════════════════
    //  回答処理（§10 初回 / §11 2回目以降）
    // ═══════════════════════════════════════════

    private void ProcessAnswer(bool isKnown)
    {
        if (currentQuestion == null) return;
        if (isProcessing) return; // 連打防止

        isProcessing = true;
        knownButton.interactable = false;
        unknownButton.interactable = false;

        DateTime now = DateTime.Now;
        int qid = currentQuestion.id;

        float bS = 0f, bK = 0f, aS, aK;

        if (userQuestions.TryGetValue(qid, out UserQuestion uq))
        {
            // ── 2回目以降 ──
            bS = uq.strength;
            bK = uq.kValue;

            uq.strength += isKnown ? STR_DELTA : -STR_DELTA;
            uq.strength = Mathf.Clamp(uq.strength, STR_MIN, STR_MAX);

            uq.kValue *= isKnown ? K_KNOWN : K_UNKNOWN;
            uq.kValue = Mathf.Clamp(uq.kValue, K_MIN, K_MAX);

            uq.reviewCount++;
            if (isKnown) uq.knownCount++; else uq.unknownCount++;

            uq.lastReviewTicks = now.Ticks;
            uq.nextReviewTicks = CalcNextReview(uq.strength, uq.kValue, now).Ticks;

            aS = uq.strength;
            aK = uq.kValue;

            LogReview(qid, isKnown, bS, aS, bK, aK, now);
            SendAnswerToApi(qid, isKnown, false, aS, aK, bS, bK);
        }
        else
        {
            // ── 初回学習 ──
            float s = isKnown ? FIRST_K_S : FIRST_U_S;

            var newUq = new UserQuestion
            {
                questionId      = qid,
                strength        = s,
                kValue          = K_INIT,
                reviewCount     = 1,
                knownCount      = isKnown ? 1 : 0,
                unknownCount    = isKnown ? 0 : 1,
                lastReviewTicks = now.Ticks,
                nextReviewTicks = CalcNextReview(s, K_INIT, now).Ticks,
                createdTicks    = now.Ticks,
            };

            userQuestions[qid] = newUq;

            LogReview(qid, isKnown, 0f, s, 0f, K_INIT, now);
            SendAnswerToApi(qid, isKnown, true, s, K_INIT, 0f, 0f);
        }

        SaveUserData();
        ShowNext();
    }

    /// <summary>API側にも回答を送信（SyncManager経由）</summary>
    private void SendAnswerToApi(int qid, bool isKnown, bool isFirst,
        float aS, float aK, float bS, float bK)
    {
        var req = new ApiManager.AnswerRequest
        {
            user_id         = api != null ? api.GetUserId() : 0,
            question_id     = qid,
            is_known        = isKnown,
            is_first        = isFirst,
            strength        = aS,
            k_value         = aK,
            before_strength = bS,
            after_strength  = aS,
            before_k        = bK,
            after_k         = aK,
            next_review_at  = CalcNextReviewStr(aS, aK),
        };

        // SyncManager経由で送信（失敗時はキューに自動保存）
        var sync = SyncManager.Instance;
        if (sync != null)
        {
            sync.SendOrQueue(req);
        }
    }

    // ═══════════════════════════════════════════
    //  数式（§4）
    // ═══════════════════════════════════════════

    private DateTime CalcNextReview(float strength, float k, DateTime now)
    {
        float h;
        if (strength <= 0.5f)
            h = NR_MIN_H;
        else
            h = Mathf.Clamp((float)(-Math.Log(0.5f / strength) / k), NR_MIN_H, NR_MAX_H);

        return now.AddHours(h);
    }

    private string CalcNextReviewStr(float strength, float k)
    {
        return CalcNextReview(strength, k, DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss");
    }

    private void LogReview(int qid, bool isKnown,
        float bS, float aS, float bK, float aK, DateTime now)
    {
        reviewLogs.Add(new ReviewLog
        {
            questionId     = qid,
            isKnown        = isKnown,
            beforeStrength = bS,
            afterStrength  = aS,
            beforeK        = bK,
            afterK         = aK,
            createdTicks   = now.Ticks,
        });
    }

    // ═══════════════════════════════════════════
    //  UI
    // ═══════════════════════════════════════════

    private void DisplayQuestion()
    {
        noText.text   = $"No. {currentQuestion.id}";
        wordText.text = currentQuestion.title;
        progressText.text = $"{userQuestions.Count} / {allQuestions.Count}";
        progressBar.value = (float)userQuestions.Count / allQuestions.Count;
        showAnswerButton.gameObject.SetActive(true);

        // 回答ボタンは無効（答えを見るまで押せない）
        knownButton.interactable = false;
        unknownButton.interactable = false;
        isProcessing = false;
    }

    private void OnShowAnswer()
    {
        if (currentQuestion == null) return;
        answerPanel.SetActive(true);
        meaningText.text = currentQuestion.answer;
        posText.text     = currentQuestion.partOfSpeech;
        showAnswerButton.gameObject.SetActive(false);

        // 答えを見たら回答ボタンを有効化
        knownButton.interactable = true;
        unknownButton.interactable = true;
    }

    private void HideAnswer()
    {
        answerPanel.SetActive(false);
        showAnswerButton.gameObject.SetActive(true);
    }

    // ═══════════════════════════════════════════
    //  公開（Stats / Daily 用）
    // ═══════════════════════════════════════════

    public int GetMasteredCount()
        => userQuestions.Values.Count(uq => uq.strength >= 0.8f && uq.knownCount >= 2);

    public int GetLearnedCount() => userQuestions.Count;
    public int GetTotalCount()   => allQuestions.Count;

    public List<ReviewLog> GetReviewLogs()                 => reviewLogs;
    public Dictionary<int, UserQuestion> GetUserQuestions() => userQuestions;
    public List<Question> GetAllQuestions()                 => allQuestions;

    // ═══════════════════════════════════════════
    //  ローカル保存 / 読込（PlayerPrefs）
    // ═══════════════════════════════════════════

    [Serializable]
    private class SaveData
    {
        public List<UserQuestion> uq;
        public List<ReviewLog> logs;
        public string refresh;
    }

    private void SaveUserData()
    {
        var data = new SaveData
        {
            uq      = userQuestions.Values.ToList(),
            logs    = reviewLogs,
            refresh = lastDailyRefresh ?? "",
        };
        PlayerPrefs.SetString("VocabSave", JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void LoadUserData()
    {
        string json = PlayerPrefs.GetString("VocabSave", "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(json);
            userQuestions.Clear();

            if (data.uq != null)
                foreach (var u in data.uq)
                    userQuestions[u.questionId] = u;

            if (data.logs != null)
                reviewLogs = data.logs;

            lastDailyRefresh = data.refresh;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[StudyManager] Load error: {e.Message}");
        }
    }
}
