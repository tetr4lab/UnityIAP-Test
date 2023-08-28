#if ALLOW_UIAP
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Purchasing;
using UnityInAppPuchaser;

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
	private bool lastHas;

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
		valid = product.Valid ();
		lastHas = !Purchaser.Inventory [product];
	}

	private void Update () {
		var has = Purchaser.Inventory [product];
		if (product != null && (lastHas != has)) {
			ID.color = Title.color = Description.color = Price.color = valid ? (has ? Color.grey : Color.white) : Color.red;
			Buy.interactable = valid && !has;
			Consume.gameObject.SetActive (Consume.interactable = valid && product.definition.type == ProductType.Consumable && has);
			lastHas = has;
		}
	}

}
#endif
