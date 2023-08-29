using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ALLOW_UIAP
using UnityEngine.Purchasing;
using UnityInAppPuchaser;
#endif
using Tetr4lab;

/// <summary>ストア</summary>
public class Store : MonoBehaviour {

	/// <summary>カタログのコンテナ</summary>
	[SerializeField] public Transform CatalogHolder = default;

	/// <summary>復元ボタン</summary>
	[SerializeField] public Button RestoreButton = default;

#if ALLOW_UIAP
	/// <summary>製品目録 (製品定義(IDと種別)の羅列)</summary>
	private readonly ProductDefinition [] products = new [] {
            new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item1", ProductType.Consumable),
            new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item2", ProductType.NonConsumable),
            new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item3", ProductType.NonConsumable),
            new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item4", ProductType.NonConsumable),
            new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item5", ProductType.NonConsumable),
            new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item6", ProductType.NonConsumable),
            new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item7", ProductType.NonConsumable),
        };
#endif

	/// <summary>起動</summary>
	private void Start () {
		WaitIndicator.Create (transform.parent);
		RestoreButton.onClick.RemoveAllListeners ();
		RestoreButton.onClick.AddListener (OnPushRestoreButton);
		CreateCatalog ();
	}

	/// <summary>初期化とカタログの生成</summary>
	private async void CreateCatalog (bool force = false) {
		WaitIndicator.display = true;
#if ALLOW_UIAP
		if (!Purchaser.Valid) {
			// 未初期化なら初期化完了を待機
			await Purchaser.InitAsync (products);
		}
		if (Purchaser.Valid && (Catalog.Count == null || force)) {
			// 初期化できていて、未生成または強制なら、(再)生成
			Catalog.Create (CatalogHolder, OnPushBuyButon, OnPushConsumeButton);
			if (!force && Purchaser.Valid) {
				WaitIndicator.display = false;
			}
		}
#endif
	}

#if ALLOW_UIAP
	/// <summary>購入ボタン</summary>
	public async void OnPushBuyButon (Product product) {
		WaitIndicator.display = true;
		await Purchaser.PurchaseAsync (product);
		if (Purchaser.result == PurchaseResult.UserCancelled) {
			// ユーザによるキャンセル (ただし、エディタでは挙動が異なる)
			WaitIndicator.display = false;
		} else {
			ModalDialog.Create (
				transform.parent,
				$"{Purchaser.result}\n{product.metadata.shortTitle ()}の購入に{((Purchaser.result == PurchaseResult.SUCCESS) ? "成功" : "失敗")}しました。",
				() => WaitIndicator.display = false
			);
		}
	}

	/// <summary>消費ボタン</summary>
	public void OnPushConsumeButton (Product product) {
		WaitIndicator.display = true;
		var result = Purchaser.ConfirmPendingPurchase (product);
		ModalDialog.Create (
			transform.parent,
			$"{product.metadata.shortTitle ()}の消費に{((result && !Purchaser.Inventory [product]) ? "成功" : "失敗")}しました。",
			() => WaitIndicator.display = false
		);
	}

	/// <summary>復元ボタン</summary>
	public void OnPushRestoreButton () {
		if (!Purchaser.Valid) { return; } // 未初期化なら離脱
		WaitIndicator.display = true;
		Purchaser.Restore ((success, message) => {
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
        if (!Purchaser.Valid && Purchaser.Status == PurchaseStatus.OFFLINE && Tetr4labUtility.IsNetworkAvailable) {
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
