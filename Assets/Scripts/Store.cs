using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ALLOW_UIAP
using UnityEngine.Purchasing;
using Tetr4lab.UnityEngine.InAppPuchaser;
#endif
using Tetr4lab.UnityEngine;

/// <summary>ストア</summary>
public class Store : MonoBehaviour {

	/// <summary>カタログのコンテナ</summary>
	[SerializeField] public Transform CatalogHolder = default;

	/// <summary>復元ボタン</summary>
	[SerializeField] public Button RestoreButton = default;

	/// <summary>情報表示テキスト</summary>
	[SerializeField] public Text InfoPanel = default;

#if ALLOW_UIAP
	/// <summary>製品目録 (製品定義(IDと種別)の羅列)</summary>
	private readonly ProductDefinition [] products = new [] {
            //new ProductDefinition ("org.tetr4lab.unityiaptest.item1", ProductType.Consumable),
            new ProductDefinition ("org.tetr4lab.unityiaptest.item2", ProductType.Consumable),
            new ProductDefinition ("org.tetr4lab.unityiaptest.item3", ProductType.NonConsumable),
            new ProductDefinition ("org.tetr4lab.unityiaptest.item4", ProductType.NonConsumable),
            new ProductDefinition ("org.tetr4lab.unityiaptest.item5", ProductType.NonConsumable),
            new ProductDefinition ("org.tetr4lab.unityiaptest.item6", ProductType.NonConsumable),
            new ProductDefinition ("org.tetr4lab.unityiaptest.item7", ProductType.Consumable),
            new ProductDefinition ("org.tetr4lab.unityiaptest.item8", ProductType.Consumable),
        };
#endif

	/// <summary>起動</summary>
	private void Start () {
		WaitIndicator.Create (transform.parent);
		RestoreButton.onClick.RemoveAllListeners ();
		RestoreButton.onClick.AddListener (OnPushRestoreButton);
		CreateCatalog ();
	}

    /// <summary>生成中</summary>
    private bool isCreating;

	/// <summary>初期化とカタログの生成</summary>
	private async void CreateCatalog (bool force = false) {
#if ALLOW_UIAP
        isCreating = true;
        if (Purchaser.IsValid && !force) { return; } // 初期化済み
		WaitIndicator.display = true;
        if (!Purchaser.IsValid) {
            await Purchaser.InitializeAsync (Purchaser.Status == PurchaseStatus.NOTINIT ? products : null);
        }
        InfoPanel.text = $"{Purchaser.Status}";
        if (WaitIndicator.display && (Purchaser.Status == PurchaseStatus.AVAILABLE || Purchaser.Status == PurchaseStatus.OFFLINE || Purchaser.Status == PurchaseStatus.UNAVAILABLE)) {
            // オフラインの場合を含めて、いったんは初期化された、または完全に失敗した
            WaitIndicator.display = false;
        }
        if (Purchaser.IsValid && (Catalog.Count == null || force)) {
            // 初期化できていて、未生成または強制なら、(再)生成
            Catalog.Create (CatalogHolder, OnPushBuyButon, OnPushConsumeButton);
        }
        Debug.Log ($"CreateCatalog {Purchaser.Status} {Catalog.Count}");
        isCreating = false;
#endif
    }

#if ALLOW_UIAP
    /// <summary>購入ボタン</summary>
    public async void OnPushBuyButon (Product product) {
		WaitIndicator.display = true;
		await Purchaser.PurchaseAsync (product, success => {
            InfoPanel.text = success ? "Success" : $"{Purchaser.Result}";
        });
		if (Purchaser.Result == PurchaseFailureReason.UserCancelled) {
            // ユーザによるキャンセル(UserCancelledでなくOrderCancelledが戻る)
            WaitIndicator.display = false;
		} else {
			ModalDialog.Create (
				transform.parent,
                $"{Purchaser.Result}\n{product.metadata.shortTitle ()}の購入{((Purchaser.Result.IsSuccess) ? Purchaser.Result == PurchaseResult.Deferring ? "は承認待ちになり" : "に成功し" : "に失敗し")}ました。",
				() => WaitIndicator.display = false
			);
		}
	}

	/// <summary>消費ボタン</summary>
	public async void OnPushConsumeButton (Product product) {
		WaitIndicator.display = true;
		var success = await Purchaser.ConfirmPurchaseAsync (product) && !Purchaser.IsConsumable (product.definition.id);
		InfoPanel.text = Purchaser.Result.ToString ();
		ModalDialog.Create (
			transform.parent,
			$"{product.metadata.shortTitle ()}の消費に{Purchaser.Result.ToJString ()}しました。",
			() => WaitIndicator.display = false
		);
	}

	/// <summary>復元ボタン</summary>
	public async void OnPushRestoreButton () {
		if (!Purchaser.IsValid) { return; } // 未初期化なら離脱
		WaitIndicator.display = true;
		await Purchaser.RestoreAsync ((success, message) => {
			InfoPanel.text = success ? "Restored" : "Failure";
			if (success) {
				// カタログを再生成
				CreateCatalog (true);
			}
			ModalDialog.Create (
				transform.parent,
				success ? "リストアしました。" : $"リストアに失敗しました。\nネットワーク接続を確認してください。\n{message}",
				() => WaitIndicator.display = false
			);
		});
	}

	/// <summary>駆動</summary>
	private void Update () {
        if (!isCreating && !Purchaser.IsValid && Purchaser.Status == PurchaseStatus.OFFLINE && Tetr4labUtility.IsNetworkAvailable) {
			// オンラインになったので遅延初期化を実施
			CreateCatalog ();
		}
	}
#else
    public void OnPushBuyButon (object product) { }
	public void OnPushConsumeButton (object product) { }
	public void OnPushRestoreButton () { }
#endif

}
