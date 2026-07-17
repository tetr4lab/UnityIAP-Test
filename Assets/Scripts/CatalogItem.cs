#if ALLOW_UIAP
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Purchasing;
using Tetr4lab.UnityEngine.InAppPuchaser;

/// <summary>表示用アイテム目録の要素</summary>
public class CatalogItem : MonoBehaviour {

    #region Static

	private static GameObject prefab;

	public static CatalogItem Create (Transform parent, Product product, UnityAction<Product> onPushBuyButton, UnityAction<Product> onPushConsumeButton) {
		if (prefab == null) {
			prefab = Resources.Load<GameObject> ("CatalogItem");
		}
		if (product != null) {
			var instance = Instantiate (prefab, parent).GetComponent<CatalogItem> ();
			instance.init (product, onPushBuyButton, onPushConsumeButton);
			return instance;
		}
		return null;
	}

    #endregion

	[SerializeField] private Text ID = default;
	[SerializeField] private Text Title = default;
	[SerializeField] private Text Description = default;
	[SerializeField] private Text Price = default;
	[SerializeField] private Button Buy = default;
	[SerializeField] private Button Consume = default;

	private Product product;
	private bool valid;
	private EntitlementStatus lastEntitlement;

	private void init (Product product, UnityAction<Product> onPushBuyButton, UnityAction<Product> onPushConsumeButton) {
		this.product = product;
		ID.text = product.definition.id;
		Title.text = product.metadata.localizedTitle;
		Description.text = product.metadata.localizedDescription;
		Price.text = product.metadata.localizedPriceString;
		Buy.onClick.RemoveAllListeners ();
		Buy.onClick.AddListener (() => onPushBuyButton (product));
		Consume.onClick.RemoveAllListeners ();
		Consume.onClick.AddListener (() => onPushConsumeButton (product));
		valid = product.IsValid ();
		lastEntitlement = EntitlementStatus.Unknown;
	}

	private void Update () {
		var entitlement = Purchaser.CheckEntitlement (product.definition.id);
		if (product != null && (lastEntitlement != entitlement)) {
            Debug.Log ($"所有状態: {ID.text} {lastEntitlement} -> {entitlement}");
			ID.color = Title.color = Description.color = Price.color = valid ? entitlement switch {
                EntitlementStatus.NotEntitled => Color.white, // 不所持
                EntitlementStatus.FullyEntitled => Color.green, // 所持
                EntitlementStatus.EntitledUntilConsumed => Color.cyan, // 未消費
                EntitlementStatus.EntitledButNotFinished => Color.yellow, // 承認待ち
                _ => Color.grey,
            } : Color.red;
			Buy.interactable = valid && entitlement == EntitlementStatus.NotEntitled;
            Consume.interactable = valid && entitlement == EntitlementStatus.EntitledUntilConsumed;
            Consume.gameObject.SetActive (Consume.interactable);
			lastEntitlement = entitlement;
		}
	}

}
#endif
