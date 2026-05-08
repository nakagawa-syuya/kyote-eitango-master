using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Unity ↔ PHP API 通信管理
/// </summary>
public class ApiManager : MonoBehaviour
{
    [Header("API Settings")]
    [SerializeField] private string baseUrl = "https://yourserver.com/api";

    private int userId = 0;

    public static ApiManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // PlayerPrefsからuserIdを復元
            if (userId <= 0 && UserManager.HasUserId())
            {
                userId = UserManager.GetUserId();
                Debug.Log($"[API] userId復元: {userId}");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetUserId(int id) { userId = id; }
    public int  GetUserId()       { return userId; }

    // ═══════════════════════════════════════════
    //  JSON応答クラス
    // ═══════════════════════════════════════════

    [Serializable] public class ApiResponse
    {
        public bool success;
        public string message;
    }

    [Serializable] public class RegisterResponse
    {
        public bool success;
        public int user_id;
        public string ab_group;
        public string message;
    }

    [Serializable] public class DailyRefreshResponse
    {
        public bool success;
        public bool refreshed;
    }

    [Serializable] public class ReviewCountResponse
    {
        public bool success;
        public int count;
    }

    [Serializable] public class TodayNewCountResponse
    {
        public bool success;
        public int count;
    }

    [Serializable] public class QuestionResponse
    {
        public bool success;
        public bool found;
        public int id;
        public string title;
        public string answer;
        public string part_of_speech;
        public int level;
        public float strength;
        public float k_value;
        public int review_count;
        public int known_count;
        public int unknown_count;
    }

    [Serializable] public class AnswerRequest
    {
        public int user_id;
        public int question_id;
        public bool is_known;
        public bool is_first;
        public float strength;
        public float k_value;
        public float before_strength;
        public float after_strength;
        public float before_k;
        public float after_k;
        public string next_review_at;
    }

    [Serializable] public class StatsResponse
    {
        public bool success;
        public int mastered;
        public int learned;
        public int total;
    }

    [Serializable] public class WeeklyRecord
    {
        public string d;
        public int total;
        public int correct;
        public float rate;
    }

    [Serializable] public class WeeklyResponse
    {
        public bool success;
        public List<WeeklyRecord> data;
    }

    // ═══════════════════════════════════════════
    //  ユーザー登録
    // ═══════════════════════════════════════════

    public void Register(Action<bool> callback)
    {
        string deviceId = UserManager.GetDeviceId();
        string json = $"{{\"device_id\":\"{deviceId}\"}}";

        StartCoroutine(PostRequest(
            $"{baseUrl}/register.php",
            json,
            (success, resJson) =>
            {
                if (!success)
                {
                    Debug.LogWarning("[API] Register通信失敗");
                    callback?.Invoke(false);
                    return;
                }

                var res = JsonUtility.FromJson<RegisterResponse>(resJson);
                if (res.success && res.user_id > 0)
                {
                    userId = res.user_id;
                    UserManager.SaveUserId(res.user_id);

                    // A/Bグループを保存
                    string group = string.IsNullOrEmpty(res.ab_group) ? "A" : res.ab_group;
                    PlayerPrefs.SetString("AbGroup", group);
                    PlayerPrefs.Save();

                    Debug.Log($"[API] Register成功: user_id={res.user_id} group={group}");
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.LogWarning($"[API] Register失敗: {res.message}");
                    callback?.Invoke(false);
                }
            }
        ));
    }

    // ═══════════════════════════════════════════
    //  アカウント削除（App Store要件）
    // ═══════════════════════════════════════════

    /// <summary>
    /// サーバーから全データを削除する。
    /// 成功したらローカルのPlayerPrefsもクリアすること。
    /// </summary>
    public void DeleteAccount(Action<bool> callback)
    {
        // userIdが0の場合、PlayerPrefsから復元を試みる
        if (userId <= 0)
        {
            userId = UserManager.GetUserId();
        }

        if (userId <= 0)
        {
            Debug.LogWarning("[API] DeleteAccount: userIdが無効");
            callback?.Invoke(false);
            return;
        }

        string json = $"{{\"user_id\":{userId}}}";

        StartCoroutine(PostRequest(
            $"{baseUrl}/delete_account.php",
            json,
            (success, resJson) =>
            {
                if (!success)
                {
                    Debug.LogWarning("[API] DeleteAccount通信失敗");
                    callback?.Invoke(false);
                    return;
                }

                var res = JsonUtility.FromJson<ApiResponse>(resJson);
                if (res.success)
                {
                    Debug.Log("[API] DeleteAccount成功");
                    userId = 0;
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.LogWarning($"[API] DeleteAccount失敗: {res.message}");
                    callback?.Invoke(false);
                }
            }
        ));
    }

    // ═══════════════════════════════════════════
    //  §8 起動時一括更新
    // ═══════════════════════════════════════════

    public void DailyRefresh(Action<bool> callback)
    {
        StartCoroutine(PostRequest(
            $"{baseUrl}/daily_refresh.php",
            $"{{\"user_id\":{userId}}}",
            (success, json) =>
            {
                if (success)
                {
                    var res = JsonUtility.FromJson<DailyRefreshResponse>(json);
                    callback?.Invoke(res.success);
                }
                else
                {
                    callback?.Invoke(false);
                }
            }
        ));
    }

    // ═══════════════════════════════════════════
    //  §9 件数取得
    // ═══════════════════════════════════════════

    public void GetReviewCount(Action<int> callback)
    {
        StartCoroutine(GetRequest(
            $"{baseUrl}/review_count.php?user_id={userId}",
            (success, json) =>
            {
                if (success)
                    callback?.Invoke(JsonUtility.FromJson<ReviewCountResponse>(json).count);
                else
                    callback?.Invoke(0);
            }
        ));
    }

    public void GetTodayNewCount(Action<int> callback)
    {
        StartCoroutine(GetRequest(
            $"{baseUrl}/today_new_count.php?user_id={userId}",
            (success, json) =>
            {
                if (success)
                    callback?.Invoke(JsonUtility.FromJson<TodayNewCountResponse>(json).count);
                else
                    callback?.Invoke(0);
            }
        ));
    }

    // ═══════════════════════════════════════════
    //  §9 問題取得
    // ═══════════════════════════════════════════

    public void GetReviewQuestion(Action<QuestionResponse> callback)
        => FetchQuestion($"{baseUrl}/get_question.php?user_id={userId}&type=review", callback);

    public void GetNewQuestion(Action<QuestionResponse> callback)
        => FetchQuestion($"{baseUrl}/get_question.php?user_id={userId}&type=new", callback);

    public void GetWeakQuestion(Action<QuestionResponse> callback)
        => FetchQuestion($"{baseUrl}/get_question.php?user_id={userId}&type=weak", callback);

    private void FetchQuestion(string url, Action<QuestionResponse> callback)
    {
        StartCoroutine(GetRequest(url, (success, json) =>
        {
            if (success)
                callback?.Invoke(JsonUtility.FromJson<QuestionResponse>(json));
            else
                callback?.Invoke(null);
        }));
    }

    // ═══════════════════════════════════════════
    //  §10, §11 回答送信
    // ═══════════════════════════════════════════

    public void SendAnswer(AnswerRequest req, Action<bool> callback)
    {
        req.user_id = userId;
        string json = JsonUtility.ToJson(req);
        Debug.Log($"[API] SendAnswer開始: userId={userId} qid={req.question_id}");
        Debug.Log($"[API] SendAnswer JSON: {json}");
        StartCoroutine(PostRequest(
            $"{baseUrl}/answer.php",
            json,
            (success, resJson) =>
            {
                Debug.Log($"[API] SendAnswer完了: success={success} response={resJson}");
                if (success)
                    callback?.Invoke(JsonUtility.FromJson<ApiResponse>(resJson).success);
                else
                    callback?.Invoke(false);
            }
        ));
    }

    // ═══════════════════════════════════════════
    //  §12 Stats
    // ═══════════════════════════════════════════

    public void GetStats(Action<StatsResponse> callback)
    {
        StartCoroutine(GetRequest(
            $"{baseUrl}/stats.php?user_id={userId}",
            (success, json) =>
            {
                if (success) callback?.Invoke(JsonUtility.FromJson<StatsResponse>(json));
                else callback?.Invoke(null);
            }
        ));
    }

    public void GetWeekly(Action<WeeklyResponse> callback)
    {
        StartCoroutine(GetRequest(
            $"{baseUrl}/weekly.php?user_id={userId}",
            (success, json) =>
            {
                if (success) callback?.Invoke(JsonUtility.FromJson<WeeklyResponse>(json));
                else callback?.Invoke(null);
            }
        ));
    }

    // ── 連続学習日数 ──

    [Serializable] public class StreakResponse
    {
        public bool success;
        public int streak;
    }

    public void GetStreak(Action<StreakResponse> callback)
    {
        StartCoroutine(GetRequest(
            $"{baseUrl}/streak.php?user_id={userId}",
            (success, json) =>
            {
                if (success) callback?.Invoke(JsonUtility.FromJson<StreakResponse>(json));
                else callback?.Invoke(null);
            }
        ));
    }

    // ── レベル別習得数 ──

    [Serializable] public class LevelItem
    {
        public int level;
        public int total;
        public int mastered;
    }

    [Serializable] public class LevelStatsResponse
    {
        public bool success;
        public List<LevelItem> levels;
    }

    public void GetLevelStats(Action<LevelStatsResponse> callback)
    {
        StartCoroutine(GetRequest(
            $"{baseUrl}/level_stats.php?user_id={userId}",
            (success, json) =>
            {
                if (success) callback?.Invoke(JsonUtility.FromJson<LevelStatsResponse>(json));
                else callback?.Invoke(null);
            }
        ));
    }

    // ── カレンダー（月別） ──

    [Serializable] public class CalendarDay
    {
        public string d;
        public int total;
        public int correct;
    }

    [Serializable] public class CalendarResponse
    {
        public bool success;
        public int year;
        public int month;
        public List<CalendarDay> data;
    }

    public void GetCalendar(int year, int month, Action<CalendarResponse> callback)
    {
        StartCoroutine(GetRequest(
            $"{baseUrl}/calendar.php?user_id={userId}&year={year}&month={month}",
            (success, json) =>
            {
                if (success) callback?.Invoke(JsonUtility.FromJson<CalendarResponse>(json));
                else callback?.Invoke(null);
            }
        ));
    }

    // ═══════════════════════════════════════════
    //  HTTP共通
    // ═══════════════════════════════════════════

    private IEnumerator GetRequest(string url, Action<bool, string> callback)
    {
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                callback(true, req.downloadHandler.text);
            else
            {
                Debug.LogWarning($"[API] GET failed: {url} / {req.error}");
                callback(false, "");
            }
        }
    }

    private IEnumerator PostRequest(string url, string jsonBody, Action<bool, string> callback)
    {
        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                callback(true, req.downloadHandler.text);
            else
            {
                Debug.LogWarning($"[API] POST failed: {url} / {req.error}");
                callback(false, "");
            }
        }
    }
}
