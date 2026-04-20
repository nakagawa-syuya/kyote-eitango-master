using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 設定画面マネージャー
/// - アカウント削除機能（App Store審査要件）
/// - プライバシーポリシーへのリンク
/// - 利用規約へのリンク
/// - バージョン表示
/// </summary>
public class SettingsManager : MonoBehaviour
{
    // ═══════════════════════════════════════════
    //  Inspector参照
    // ═══════════════════════════════════════════

    [Header("Buttons")]
    [SerializeField] private Button btnDeleteAccount;
    [SerializeField] private Button btnPrivacyPolicy;
    [SerializeField] private Button btnTerms;
    [SerializeField] private Button btnBack;

    [Header("Delete Confirmation Dialog")]
    [SerializeField] private GameObject confirmDialog;      // 確認ダイアログ（通常は非表示）
    [SerializeField] private Button btnConfirmDelete;
    [SerializeField] private Button btnCancelDelete;
    [SerializeField] private TextMeshProUGUI confirmMessage;

    [Header("Processing Overlay")]
    [SerializeField] private GameObject processingOverlay;  // 処理中表示（通常は非表示）

    [Header("Info")]
    [SerializeField] private TextMeshProUGUI versionText;

    [Header("External URLs")]
    [SerializeField] private string privacyPolicyUrl = "https://yoursite.com/privacy";
    [SerializeField] private string termsUrl          = "https://yoursite.com/terms";

    [Header("Scene Names")]
    [SerializeField] private string mainSceneName = "Ques";

    // ═══════════════════════════════════════════
    //  初期化
    // ═══════════════════════════════════════════

    private void Start()
    {
        if (btnDeleteAccount != null)
            btnDeleteAccount.onClick.AddListener(ShowDeleteConfirmation);

        if (btnPrivacyPolicy != null)
            btnPrivacyPolicy.onClick.AddListener(() => Application.OpenURL(privacyPolicyUrl));

        if (btnTerms != null)
            btnTerms.onClick.AddListener(() => Application.OpenURL(termsUrl));

        if (btnBack != null)
            btnBack.onClick.AddListener(() => SceneManager.LoadScene(mainSceneName));

        if (btnConfirmDelete != null)
            btnConfirmDelete.onClick.AddListener(ExecuteDeleteAccount);

        if (btnCancelDelete != null)
            btnCancelDelete.onClick.AddListener(HideDeleteConfirmation);

        // 初期状態
        if (confirmDialog != null) confirmDialog.SetActive(false);
        if (processingOverlay != null) processingOverlay.SetActive(false);

        // バージョン表示
        if (versionText != null)
            versionText.text = $"Version {Application.version}";
    }

    // ═══════════════════════════════════════════
    //  アカウント削除
    // ═══════════════════════════════════════════

    /// <summary>削除確認ダイアログを表示</summary>
    private void ShowDeleteConfirmation()
    {
        if (confirmDialog == null) return;

        confirmDialog.SetActive(true);

        if (confirmMessage != null)
        {
            confirmMessage.text =
                "本当にアカウントを削除しますか？\n\n" +
                "・全ての学習記録が完全に削除されます\n" +
                "・この操作は取り消せません\n" +
                "・再度ご利用の場合は最初からやり直しになります";
        }
    }

    private void HideDeleteConfirmation()
    {
        if (confirmDialog != null)
            confirmDialog.SetActive(false);
    }

    /// <summary>削除実行</summary>
    private void ExecuteDeleteAccount()
    {
        HideDeleteConfirmation();

        if (processingOverlay != null)
            processingOverlay.SetActive(true);

        var api = ApiManager.Instance;
        if (api == null)
        {
            Debug.LogError("[SettingsManager] ApiManager未設定");
            LocalDeleteAndRestart();
            return;
        }

        api.DeleteAccount(success =>
        {
            if (success)
            {
                Debug.Log("[SettingsManager] サーバー削除成功");
            }
            else
            {
                Debug.LogWarning("[SettingsManager] サーバー削除失敗。ローカルのみクリアします");
            }

            // サーバー成否に関わらずローカルは必ずクリア
            LocalDeleteAndRestart();
        });
    }

    /// <summary>ローカルデータを全削除してアプリを初期状態に戻す</summary>
    private void LocalDeleteAndRestart()
    {
        // PlayerPrefsを全クリア
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("[SettingsManager] ローカルデータ削除完了");

        // 最初の画面に戻る（再起動相当の動作）
        // アプリ再起動はiOSだと禁止されているので、シーン再読込で代用
        SceneManager.LoadScene(mainSceneName);
    }
}
