using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 処理中の表示と操作のブロック
///		基本的な使い方
///			WaitIndicator.display = true; // 表示
///			WaitIndicator.display = false; // 消去
///		オプション (あらかじめ実行することで設定する)
///			WaitIndicator.Create (transform.parent); // 親
///			WaitIndicator.Create (message: "busy..."); // 文言
///			WaitIndicator.Create (transform.parent, "busy..."); // 親と文言
/// </summary>
public class WaitIndicator : MonoBehaviour {

	#region Static

	/// <summary>プレファブ</summary>
	private static GameObject prefab;
	/// <summary>シングルトンインスタンス</summary>
	private static WaitIndicator instance;
	/// <summary>最初に見つかったキャンバス</summary>
	private static Transform canvas;

	/// <summary>
	/// 表示の切り替え
	///		未初期化ならインスタンスを自動生成
	/// </summary>
	public static bool display {
		get => instance?.gameObject.activeSelf == true;
		set {
			instance ??= Create ();
			instance.gameObject.SetActive (value);
        }
    }

	/// <summary>明示的な(再)初期化</summary>
	/// <param name="parent">コンテナ 与えられなければ適当に探したキャンバスを使用</param>
	/// <param name="message">代替テキスト 与えられなければプレファブのまま</param>
	/// <returns>生成されたインスタンス</returns>
	public static WaitIndicator Create (Transform parent = null, string message = null) {
		prefab ??= Resources.Load<GameObject> ("WaitIndicator");
		canvas ??= FindObjectOfType<Canvas> ()?.transform;
		if (instance) {
			Destroy (instance.gameObject);
			instance = null;
		}
		instance = Instantiate (prefab, parent ?? canvas).GetComponent<WaitIndicator> ();
		instance.init (message);
		return instance;
	}

	#endregion

	/// <summary>メッセージが格納されるUIテキスト</summary>
	[SerializeField] private Text Message = default;

	/// <summary>インスタンスの初期化</summary>
	/// <param name="message">代替テキスト</param>
	private void init (string message) {
		if (!string.IsNullOrEmpty (message) && Message != null) {
			Message.text = message;
		}
	}

}