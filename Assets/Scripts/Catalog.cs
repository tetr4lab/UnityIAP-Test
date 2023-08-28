#if ALLOW_UIAP
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Purchasing;
using UnityInAppPuchaser;

/// <summary>表示用アイテム目録</summary>
public class Catalog : MonoBehaviour {

	#region Static

	private static GameObject prefab;
	private static Catalog instance;
	private static ScrollRect ScrollRect;

	/// <summary>掲載アイテム数 (未初期化ならnull)</summary>
	public static int? Count => instance?.transform.childCount;

	/// <summary>生成</summary>
	/// <param name="parent">コンテナ</param>
	/// <returns>生成されたカタログ</returns>
	public static Catalog Create (Transform parent, UnityAction<Product> onPushBuyButton, UnityAction<Product> onPushConsumeButton) {
		if (prefab == null) {
			prefab = Resources.Load<GameObject> ("Catalog");
		}
		if (instance != null) {
			// シングルトン挙動
			Destroy (instance.gameObject);
			instance = null;
		}
		instance = Instantiate (prefab, parent).GetComponent<Catalog> ();
		instance.init (onPushBuyButton, onPushConsumeButton);
		return instance;
	}

	#endregion

	private void init (UnityAction<Product> onPushBuyButton, UnityAction<Product> onPushConsumeButton) {
		ScrollRect = GetComponent<ScrollRect> ();
		foreach (var product in Purchaser.Products.all) {
			CatalogItem.Create (ScrollRect.content, product, onPushBuyButton, onPushConsumeButton);
		}
	}

}
#endif
