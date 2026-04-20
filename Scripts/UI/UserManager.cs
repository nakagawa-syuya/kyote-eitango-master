using System;
using UnityEngine;

/// <summary>
/// ユーザー識別管理（static）
/// - device_id: 初回起動時に自動生成、PlayerPrefsに永続化
/// - user_id:   サーバー登録時に払い出される値
/// </summary>
public static class UserManager
{
    private const string DEVICE_ID_KEY = "device_id";
    private const string USER_ID_KEY   = "user_id";

    /// <summary>
    /// 端末固有のdevice_idを取得。初回はGuidを生成して保存。
    /// </summary>
    public static string GetDeviceId()
    {
        string id = PlayerPrefs.GetString(DEVICE_ID_KEY, "");
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(DEVICE_ID_KEY, id);
            PlayerPrefs.Save();
            Debug.Log($"[UserManager] 新規device_id生成: {id}");
        }
        return id;
    }

    /// <summary>サーバーから払い出されたuser_idを保存</summary>
    public static void SaveUserId(int userId)
    {
        PlayerPrefs.SetInt(USER_ID_KEY, userId);
        PlayerPrefs.Save();
    }

    /// <summary>保存済みのuser_idを取得。なければ0</summary>
    public static int GetUserId()
    {
        return PlayerPrefs.GetInt(USER_ID_KEY, 0);
    }

    /// <summary>user_idが保存済みか</summary>
    public static bool HasUserId()
    {
        return PlayerPrefs.HasKey(USER_ID_KEY);
    }
}
