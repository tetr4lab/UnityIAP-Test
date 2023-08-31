#define UNITY_PURCHASING_4_8_0_OR_HIGHER
#define UNITY_PURCHASING_4_6_0_OR_HIGHER
#if ALLOW_UIAP
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
using UnityEngine.Purchasing.Extension;

using Tetr4lab;

namespace UnityInAppPuchaser {
	/// <summary>IAPの利用可能状況</summary>
	public enum PurchaseStatus {
		NOTINIT = 0, // 未初期化状態`
		AVAILABLE, // 利用可能
		UNAVAILABLE, // 利用不能
		OFFLINE, // オフライン
	}

	/// <summary>購入の結果</summary>
	public enum PurchaseResult {
		ERROR = -1,					// 購入開始失敗
		SUCCESS = 0,				// 購入成功

		PurchasingUnavailable,		// システムの購入機能が利用できません。
		ExistingPurchasePending,	// 新たに購入をリクエストしましたが、すでに購入処理中でした。
		ProductUnavailable,			// ストアで購入できる商品ではありません。
		SignatureInvalid,			// 課金レシートのシグネチャ検証に失敗しました。
		UserCancelled,				// ユーザは購入の続行よりキャンセルを選びました。
		PaymentDeclined,			// 支払いに問題がありました。
		DuplicateTransaction,		// 重複トランザクションエラー.
		Unknown,					// 認識不能な問題のある購入のすべて。
		NotValid,					// 初期化できていない
		Disconnected,				// ネット接続がない
	}

    /// <summary>課金処理</summary>
#if UNITY_PURCHASING_4_8_0_OR_HIGHER
	public class Purchaser : IDetailedStoreListener {
#else
    public class Purchaser : IStoreListener {
#endif

		#region Static
		/// <summary>シングルトン</summary>
		private static Purchaser instance;

		/// <summary>所有目録 製品の課金状況一覧、消費タイプは未消費を表す</summary>
		public static Inventory Inventory { get; private set; }

		/// <summary>有効 初期化が完了している</summary>
		public static bool Valid => (instance != null && instance.valid);

		/// <summary>初期化状況</summary>
		public static PurchaseStatus Status { get; private set; } = PurchaseStatus.NOTINIT;

		/// <summary>製品目録 初期化時の製品IDに対してストアから得た情報</summary>
		public static ProductCollection Products => Valid ? instance.controller.products : null;

		/// <summary>有効 購入が完了している</summary>
		public static bool PurchaseValid => Valid && instance.purchasing == false;

        /// <summary>IDから製品を得る</summary>
        /// <param name="productID">製品ID</param>
        /// <returns>製品</returns>
        public static Product Product (string productID) => !string.IsNullOrEmpty (productID) && Valid ? instance.controller.products.WithID (productID) : null;

        /// <summary>購入失敗の理由</summary>
        public static PurchaseResult Result { get; private set; }

        /// <summary>初期化通過時の処理 (複数の実行機会)</summary>
        private static Action<bool> onInitialized;

        /// <summary>クラスを初期化を開始してコールバックを得る (ノンブロック)</summary>
        /// <param name="products">製品定義</param>
        /// <param name="onInitialized">完了時コールバックハンドラ</param>
        public static void Initialize (IEnumerable<ProductDefinition> products, Action<bool> onInitialized) => _ = InitializeAsync (products, onInitialized);

        /// <summary>製品目録キャッシュ</summary>
        private static IEnumerable<ProductDefinition> _products = null;

        /// <summary>
        /// クラスを初期化して結果を得る
        ///   クラスはシングルトンとして振る舞います。
        ///   既に初期化に成功している場合の再初期化はできません。
        ///     初回初期化時にオフラインだった場合は、オンラインになってから引数なしで呼ぶことで初期化を完了できます。
        ///     製品定義や完了時コールバックハンドラの上書きはできません。
        ///   `Status == PurchaseStatus.OFFLINE`の場合もコールバックがあります。
        /// </summary>
        /// <param name="products">製品定義</param>
        /// <param name="onInitialized">完了時コールバックハンドラ</param>
        /// <returns>結果</returns>
        public static async Task<bool> InitializeAsync (IEnumerable<ProductDefinition> products = null, Action<bool> onInitialized = null) {
			// IAPを初期化する
			products ??= _products;
			Purchaser.onInitialized ??= onInitialized;
            var collection = products as ICollection<ProductDefinition>;
            if (collection == null || collection.Count < 1) {
                Debug.LogWarning ("IAPが初期化できない");
                Status = PurchaseStatus.UNAVAILABLE;
                onInitialized?.Invoke (false);
            } else if (!Tetr4labUtility.IsNetworkAvailable) {
                Debug.LogWarning ("インターネットに接続していない");
                if (Valid) { // 既に一度は初期化されている
                } else {
                    Status = PurchaseStatus.OFFLINE;
                    onInitialized?.Invoke (false);
                }
            } else {
#if UNITY_ANDROID
                if (instance != null) { instance = null; }
#endif
                if (instance == null || Status != PurchaseStatus.AVAILABLE) {
                    // UnityPurchasingを初期化する前にUnityServicesを初期化する必要がある
                    try {
                        var options = new InitializationOptions ().SetEnvironmentName ("production");
                        await UnityServices.InitializeAsync (options);
                    }
                    catch (Exception exception) {
						Debug.LogError ($"An error occurred during services initialization. {exception}");
						instance = null;
						Status = PurchaseStatus.UNAVAILABLE;
						onInitialized?.Invoke (false);
						return false;
					}
                    instance = new Purchaser (products);
                    // 初期化完了を待つ
                    await TaskEx.DelayUntil (() => Status != PurchaseStatus.NOTINIT);
                }
            }
			var ready = Valid && Status == PurchaseStatus.AVAILABLE;
			_products = ready ? null : products; // 未完了ならキャッシュ
			return ready;
		}

		/// <summary>所有検証 有効なレシートが存在する</summary>
		private static bool possession (Product product) {
			return product.hasReceipt && ValidateReceipt (product);
		}

		/// <summary>レシート検証</summary>
		public static bool ValidateReceipt (string productID) => ValidateReceipt (Product (productID));

		/// <summary>レシート検証</summary>
		public static bool ValidateReceipt (Product product) {
			return product != null && Valid && instance.validateReceipt (product);
		}

		/// <summary>保留した課金の完了 消費タイプの指定製品の保留していた消費を完了する</summary>
		public static bool ConfirmPendingPurchase (string productID) => ConfirmPendingPurchase (Product (productID));

		/// <summary>保留した課金の完了 消費タイプの指定製品の保留していた消費を完了する</summary>
		public static bool ConfirmPendingPurchase (Product product) {
			if (product != null && Valid) {
				return instance.confirmPendingPurchase (product);
			}
			return false;
		}

		/// <summary>復元 課金情報の復元を行い、結果のコールバックを得ることができる</summary>
		public static void Restore (Action<bool, string> onRestored = null) {
			if (Valid) {
				instance.restore (onRestored);
			} else {
				onRestored?.Invoke (false, null);
			}
		}

		/// <summary>所有の確認</summary>
		/// <param name="productID">製品ID</param>
		/// <returns>製品の所有状態、製品が存在しなければnullを返す</returns>
		public static bool? IsStocked (string productID) => IsStocked (Product (productID));

        /// <summary>所有の確認</summary>
        /// <param name="product">製品</param>
        /// <returns>製品の所有状態、製品が存在しなければnullを返す</returns>
        public static bool? IsStocked (Product product) => product != null && Valid && Inventory.ContainsKey (product) ? Inventory [product] : null;

        /// <summary>課金を開始してコールバックを得る</summary>
        /// <param name="product">製品</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        public static void Purchase (Product product, Action<bool> onPurchased) => _ = PurchaseAsync (product, onPurchased);

        /// <summary>課金を開始してコールバックを得る</summary>
        /// <param name="product">製品</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        public static void Purchase (string productID, Action<bool> onPurchased) => _ = PurchaseAsync (productID, onPurchased);

        /// <summary>課金</summary>
        /// <param name="product">製品</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        /// <returns>成否</returns>
        public static async Task<bool> PurchaseAsync (Product product, Action<bool> onPurchased = null) {
			Debug.Log ($"PurchaseAsync ({product}, {onInitialized}) {product?.definition?.id}");
			if (product == null) {
                // 無効な製品
                Result = PurchaseResult.ProductUnavailable;
            } else if (!Valid && !await InitializeAsync ()) {
                // 初期化できていないので再初期化したものの失敗
                Result = PurchaseResult.NotValid;
			} else {
				instance.purchase (product);
            }
            await TaskEx.DelayUntil (() => PurchaseValid); // 購入処理完了を待つ
			var success = Result == PurchaseResult.SUCCESS;
            onPurchased?.Invoke (success);
            return success;
		}

        /// <summary>課金</summary>
        /// <param name="productID">製品ID</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        /// <returns>成否</returns>
        public static async Task<bool> PurchaseAsync (string productID, Action<bool> onPurchased = null) {
			Debug.Log ($"PurchaseAsync (productID: {productID})");
			if (string.IsNullOrEmpty (productID)) {
                // 無効な製品ID
                Result = PurchaseResult.ProductUnavailable;
            } else if (!Valid && !await InitializeAsync ()) {
				// 初期化できていないので再初期化したものの失敗
				Result = PurchaseResult.NotValid;
			} else {
				// 製品IDから製品を検出するには要初期化
				return await PurchaseAsync (Product (productID), onPurchased);
            }
            onPurchased?.Invoke (false);
            return false;
        }

        #endregion

        /// <summary>コントローラー</summary>
        private IStoreController controller;
		/// <summary>拡張プロバイダ</summary>
		private IExtensionProvider extensions;
		/// <summary>Apple拡張</summary>
		private IAppleExtensions appleExtensions;
		/// <summary>Google拡張</summary>
		private IGooglePlayStoreExtensions googlePlayStoreExtensions;
		/// <summary>AppleAppStore</summary>
		private bool isAppleAppStoreSelected;
		/// <summary>GooglePlayStore</summary>
		private bool isGooglePlayStoreSelected;
		/// <summary>検証機構</summary>
		private CrossPlatformValidator validator;
		/// <summary>有効</summary>
		private bool valid => (controller != null && controller.products != null);

        /// <summary>購入中</summary>
        private bool purchasing;

		/// <summary>コンストラクタ</summary>
		private Purchaser (IEnumerable<ProductDefinition> products) {
			Debug.Log ("Purchaser.Construct");
			Status = PurchaseStatus.NOTINIT;
			var module = StandardPurchasingModule.Instance ();
			module.useFakeStoreUIMode = FakeStoreUIMode.StandardUser;
			isGooglePlayStoreSelected = Application.platform == RuntimePlatform.Android && module.appStore == AppStore.GooglePlay;
			isAppleAppStoreSelected = Application.platform == RuntimePlatform.IPhonePlayer;// && module.appStore == AppStore.AppleAppStore;
			var builder = ConfigurationBuilder.Instance (module);
			builder.AddProducts (products);
			if (Inventory != null) { Inventory = null; }
			Inventory = new Inventory { };
			UnityPurchasing.Initialize (this, builder);
		}

		/// <summary>レシート検証</summary>
		private bool validateReceipt (Product product) {
			if (/*!valid ||*/ !product.hasReceipt) { return false; }
#if UNITY_EDITOR
			return true;
#else
			// 各Tangleクラスを得るために、あらかじめ「Services > In-App Purchasing > Receipt Validation Obfuscator...」を実行してください。
			var validator = new CrossPlatformValidator (GooglePlayTangle.Data (), AppleTangle.Data (), Application.identifier);
			try {
				var result = validator.Validate (product.receipt);
				Debug.Log ($"Purchaser.validateReceipt Receipt is valid. (id:{product.definition.id})");
#if false
                Debug.Log ("Contents:");
                foreach (IPurchaseReceipt productReceipt in result) {
                    Debug.Log (productReceipt.productID);
                    Debug.Log (productReceipt.purchaseDate);
                    Debug.Log (productReceipt.transactionID);
                    GooglePlayReceipt google = productReceipt as GooglePlayReceipt;
                    if (null != google) {
                        Debug.Log (google.purchaseState);
                        Debug.Log (google.purchaseToken);
                    }
                    AppleInAppPurchaseReceipt apple = productReceipt as AppleInAppPurchaseReceipt;
                    if (null != apple) {
                        Debug.Log (apple.originalTransactionIdentifier);
                        Debug.Log (apple.subscriptionExpirationDate);
                        Debug.Log (apple.cancellationDate);
                        Debug.Log (apple.quantity);
                    }
                }
#endif
                return true;
			} catch (IAPSecurityException ex) {
				UnityEngine.Debug.LogError ($"Purchaser.validateReceipt Invalid receipt {product.definition.id}, not unlocking content. {ex}");
				return false;
			}
#endif
        }

        /// <summary>課金開始</summary>
        private bool purchase (Product product) {
			if (!Tetr4labUtility.IsNetworkAvailable) {
				Debug.Log ("インターネットへの接続経路がない");
				Result = PurchaseResult.Disconnected;
				return false;
			}
			if (product != null && product.Valid ()) {
				Debug.Log ($"Purchaser.InitiatePurchase {product.definition.id} {product.metadata.localizedTitle} {product.metadata.localizedPriceString}");
				controller.InitiatePurchase (product);
				purchasing = true;
				return true;
			}
			Result = PurchaseResult.ERROR;
			return false;
		}

		/// <summary>保留した課金の完了</summary>
		private bool confirmPendingPurchase (Product product) {
			if (!Tetr4labUtility.IsNetworkAvailable) {
				Debug.Log ("インターネットへの接続経路がない");
				Result = PurchaseResult.Disconnected;
				return false;
			}
			if (product != null && Inventory [product] && possession (product)) {
				controller.ConfirmPendingPurchase (product);
				Inventory [product] = false;
				Debug.Log ($"Purchaser.ConfirmPendingPurchase {product.GetProperties ()}");
				Result = PurchaseResult.SUCCESS;
				return true;
			}
			Result = PurchaseResult.ERROR;
			return false;
		}

		/// <summary>復元</summary>
		private void restore (Action<bool, string> onRestored = null) {
			Debug.Log ("Purchaser.Restore");
			if (isGooglePlayStoreSelected) {
				// 不要? ref: https://docs.unity3d.com/ja/current/Manual/UnityIAPRestoringTransactions.html
				//googlePlayStoreExtensions.RestoreTransactions (success => {
				//	Debug.Log ($"Purchaser.Restored {success}");
				//	onRestored?.Invoke (success, null);
				//});
				onRestored?.Invoke (true, null);
			} else if (isAppleAppStoreSelected) {
#if UNITY_PURCHASING_4_6_0_OR_HIGHER || UNITY_PURCHASING_4_8_0_OR_HIGHER
                appleExtensions.RestoreTransactions ((success, message) => {
					Debug.Log ($"Purchaser.Restored {success} {message}");
					onRestored?.Invoke (success, message);
				});
#else
                appleExtensions.RestoreTransactions ((success) => {
                    Debug.Log ($"Purchaser.Restored {success}");
                    onRestored?.Invoke (success, null);
                });
#endif
            } else {
				onRestored?.Invoke (
#if UNITY_EDITOR
					true
#else
					false
#endif
				, null);
			}
		}

		#region Event Handler

		/// <summary>iOS 'Ask to buy' 未成年者の「承認と購入のリクエスト」 承認または却下されると通常の購入イベントが発生する</summary>
		private void OnDeferred (Product product) {
			Debug.Log ($"Purchaser.Deferred {product.GetProperties ()}");
		}

		/// <summary>初期化完了</summary>
		public async void OnInitialized (IStoreController controller, IExtensionProvider extensions) {
			await TaskEx.DelayUntil (() => instance != null); // シングルトンインスタンスの生成を待つ
			Debug.Log ($"Purchaser.Initialized ({controller}, {extensions})");
			appleExtensions = extensions.GetExtension<IAppleExtensions> ();
			appleExtensions.RegisterPurchaseDeferredListener (OnDeferred);
			googlePlayStoreExtensions = extensions.GetExtension<IGooglePlayStoreExtensions> ();
			this.controller = controller;
			this.extensions = extensions;
			if (valid) {
                Status = PurchaseStatus.AVAILABLE;
                foreach (var product in controller.products.all) {
					if (!Inventory.ContainsKey (product)) {
						Inventory [product] = possession (product);
					}
				}
            } else {
				Debug.LogWarning ($"ストアコントローラが取得できない");
                Status = PurchaseStatus.UNAVAILABLE;
            }
            onInitialized?.Invoke (valid);
        }

        /// <summary>初期化失敗</summary>
        public void OnInitializeFailed (InitializationFailureReason error, string message) {
			Debug.Log ($"Purchaser.InitializeFailed {error} {message}");
			Status = PurchaseStatus.UNAVAILABLE;
			switch (error) {
				case InitializationFailureReason.PurchasingUnavailable:
					Debug.Log ("デバイス設定でアプリ内購入が無効です。");
					//Debug.Log ("In-App Purchases disabled in device settings.");
					break;

				case InitializationFailureReason.NoProductsAvailable:
					Debug.Log ("購入できるプロダクトがありません。");
					//Debug.Log ("No products available for purchase.");
					break;

				case InitializationFailureReason.AppNotKnown:
					Debug.Log ("ストアが、このアプリケーションは「不明」と報告します。");
					//Debug.Log ("The store reported the app as unknown.");
					break;
			}
            onInitialized?.Invoke (false);
        }

        /// <summary>初期化失敗 (旧形式)</summary>
        public void OnInitializeFailed (InitializationFailureReason error) => OnInitializeFailed (error);

#if UNITY_PURCHASING_4_8_0_OR_HIGHER
		/// <summary>課金失敗</summary>
		public void OnPurchaseFailed (Product product, PurchaseFailureDescription failureDescription) => OnPurchaseFailed (product, failureDescription.reason);
#endif
        /// <summary>課金失敗</summary>
        public void OnPurchaseFailed (Product product, PurchaseFailureReason reason) {
			Debug.Log ($"Purchaser.PurchaseFailed Reason={reason}\n{product.GetProperties ()}");
			switch (reason) {
				case PurchaseFailureReason.PurchasingUnavailable:
					Debug.Log ("システムの購入機能が利用できません。");
					//reason = "The system purchasing feature is unavailable.";
					Result = PurchaseResult.PurchasingUnavailable;
					break;

				case PurchaseFailureReason.ExistingPurchasePending:
					Debug.Log ("新たに購入をリクエストしましたが、すでに購入処理中でした。");
					//reason = "A purchase was already in progress when a new purchase was requested.";
					Result = PurchaseResult.ExistingPurchasePending;
					break;

				case PurchaseFailureReason.ProductUnavailable:
					Debug.Log ("ストアで購入できる商品ではありません。");
					//reason = "The product is not available to purchase on the store.";
					Result = PurchaseResult.ProductUnavailable;
					break;

				case PurchaseFailureReason.SignatureInvalid:
					Debug.Log ("課金レシートのシグネチャ検証に失敗しました。");
					//reason = "Signature validation of the purchase's receipt failed.";
					Result = PurchaseResult.SignatureInvalid;
					break;

				case PurchaseFailureReason.UserCancelled:
					Debug.Log ("ユーザは購入の続行よりキャンセルを選びました。");
					//reason = "The user opted to cancel rather than proceed with the purchase.";
					Result = PurchaseResult.UserCancelled;
					break;

				case PurchaseFailureReason.PaymentDeclined:
					Debug.Log ("支払いに問題がありました。");
					//reason = "There was a problem with the payment.";
					Result = PurchaseResult.PaymentDeclined;
					break;

				case PurchaseFailureReason.DuplicateTransaction:
					Debug.Log ("重複トランザクションエラー");
					//reason = "A duplicate transaction error when the transaction has already been completed successfully.";
					Result = PurchaseResult.DuplicateTransaction;
					break;

				case PurchaseFailureReason.Unknown:
					Debug.Log ("その他の認識不能な購入エラー");
					//reason = "A catch-all for unrecognized purchase problems.";
					Result = PurchaseResult.Unknown;
					break;
			}
			purchasing = false;
		}

		/// <summary>課金結果 有効な消耗品なら保留、それ以外は完了とする</summary>
		public PurchaseProcessingResult ProcessPurchase (PurchaseEventArgs eventArgs) {
			var validated = ValidateReceipt (eventArgs.purchasedProduct);
			Inventory [eventArgs.purchasedProduct] = validated;
			Debug.Log ($"Purchaser.ProcessPurchase {(validated ? "Validated" : "ValidationError")} {eventArgs.purchasedProduct.GetProperties ()}");
			if (valid) {
				Result = PurchaseResult.SUCCESS;
				purchasing = false;
			}
			return (validated && eventArgs.purchasedProduct.definition.type == ProductType.Consumable) ? PurchaseProcessingResult.Pending : PurchaseProcessingResult.Complete;
		}

		/// <summary>破棄</summary>
		~Purchaser () {
			Debug.Log ("Purchaser.Destruct");
			if (instance == this) {
				instance = null;
				Inventory = null;
				Status = PurchaseStatus.AVAILABLE;
				_products = null;
			}
		}

		#endregion
	}	// Purchaser

	/// <summary>製品拡張</summary>
	public static class ProductExtentions {

		/// <summary>製品諸元</summary>
		public static string GetProperties (this Product product) {
			return string.Join ("\n", new [] {
				$"id={product.definition.id} ({product.definition.storeSpecificId})",
				$"type={product.definition.type}",
				$"enabled={product.definition.enabled}",
				$"available={product.availableToPurchase}",
				$"localizedTitle={product.metadata.localizedTitle}({product.metadata.shortTitle ()})",
				$"localizedDescription={product.metadata.localizedDescription}",
				$"isoCurrencyCode={product.metadata.isoCurrencyCode}",
				$"localizedPrice={product.metadata.localizedPrice}",
				$"localizedPriceString={product.metadata.localizedPriceString}",
				$"transactionID={product.transactionID}",
				$"Receipt has={product.hasReceipt}",
				$"Purchaser.Valid={Purchaser.Valid}",
				$"Receipt validation={Purchaser.ValidateReceipt (product)}",
				$"Possession={Purchaser.Inventory [product]}",
			});
		}

		/// <summary>目録からIDで探す</summary>
		/// <param name="products">目録</param>
		/// <param name="id">ID</param>
		/// <returns>製品</returns>
		public static Product GetProduct (this Product [] products, string id) {
			var i = Array.FindIndex (products, p => p.definition.id == id);
			return (i < 0) ? null : products [i];
        }

		/// <summary>有効性 製品がストアに登録されていることを示すが、ストアで有効かどうかには拠らない</summary>
		public static bool Valid (this Product product) {
			return (product.definition.enabled && product.availableToPurchase);
		}

		/// <summary>アプリ名を含まないタイトル</summary>
		public static string shortTitle (this ProductMetadata metadata) {
			return (metadata != null && !string.IsNullOrEmpty (metadata.localizedTitle)) ? (new Regex (@"\s*\(.+\)$")).Replace (metadata.localizedTitle, "") : string.Empty;
		}

	}	// ProductExtentions

	/// <summary>productID基準でProductの所有を表現する辞書</summary>
	public class Inventory : Dictionary<string, bool> {

		/// <summary>Productによるアクセス</summary>
		public bool this [Product product] {
			get => base [product.definition.id];
			set => base [product.definition.id] = value;
		}

		/// <summary>Productによる存在確認</summary>
		public bool ContainsKey (Product product) => ContainsKey (product.definition.id);

	}	// Inventory

}	// namespace
#endif