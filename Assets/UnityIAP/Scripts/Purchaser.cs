#nullable enable
#if ALLOW_UIAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tetr4lab;
using Tetr4lab.UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Tetr4lab.UnityEngine.InAppPuchaser {
    /// <summary>IAPの利用可能状況</summary>
    public enum PurchaseStatus {
        /// <summary>未初期化状態</summary>
        NOTINIT = 0,
        /// <summary>利用可能</summary>
        AVAILABLE,
        /// <summary>利用不能</summary>
        UNAVAILABLE,
        /// <summary>オフライン</summary>
        OFFLINE,
    }

    /// <summary>購入の結果</summary>
    /// <remarks>PurchaseFailureReasonと独自の定数の参照と代入が可能な列挙型のように振る舞う</remarks>
    public class PurchaseResult {
        /// <summary>結果</summary>
        public int Result { get; private set; }
        /// <summary>失敗の理由</summary>
        public PurchaseFailureReason Reason { get; private set; }
        /// <summary>成否</summary>
        public bool IsSuccess => Result == SUCCESS || Result == Pending || Result == Deferring;
        /// <summary>該当の注文が見つからない</summary>
        public const int NotFound = -5;
        /// <summary>ネット接続がない</summary>
        public const int Disconnected = -4;
        /// <summary>他の処理中</summary>
        public const int Busy = -3;
        /// <summary>初期化できていない</summary>
        public const int Unavailable = -2;
        /// <summary>購入失敗</summary>
        public const int ERROR = -1;
        /// <summary>購入成功</summary>
        public const int SUCCESS = 0;
        /// <summary>進行中</summary>
        public const int Purchasing = 1;
        /// <summary>保留中(消費待ち)</summary>
        public const int Pending = 2;
        /// <summary>承認待機</summary>
        public const int Deferring = 3;
        /// <summary>完了確認中</summary>
        public const int Confirming = 4;
        /// <summary>結果の取得</summary>
        public static implicit operator int (PurchaseResult x) => x.Result;
        /// <summary>結果の代入</summary>
        public static implicit operator PurchaseResult (int x) => new PurchaseResult { Result = x, Reason = PurchaseFailureReason.Unknown, };
        /// <summary>理由の取得</summary>
        public static implicit operator PurchaseFailureReason (PurchaseResult x) => x.Result == ERROR ? x.Reason : PurchaseFailureReason.Unknown;
        /// <summary>理由の代入</summary>
        public static implicit operator PurchaseResult (PurchaseFailureReason x) => new PurchaseResult { Result = ERROR, Reason = x, };
        /// <summary>初期値</summary>
        public PurchaseResult () { Result = SUCCESS; Reason = PurchaseFailureReason.Unknown; }
        /// <summary>成功の名称</summary>
        private static readonly string [] resultName = { "Success", "Purchasing", "Pending", "Deferring", "Confirming", };
        /// <summary>失敗の名称</summary>
        private static readonly string [] errorName = { "Error", "Unavailable", "Busy", "Disconnected", "NotFound", };
        /// <summary>文字列化</summary>
        public override string ToString () => ToJString (false);
        /// <summary>日本語文字列化</summary>
        public string ToJString (bool jp = true) => Result >= 0 && Result < resultName.Length ? Result > 0 ? resultName [Result] : (jp? "成功" : resultName [0]) : $"{(jp ? "失敗" : errorName [0])}({(Result == ERROR ? Reason : errorName [Math.Abs (Result + 1)])})";
    }

    /// <summary>課金処理</summary>
    /// <remarks>
    /// 初期化フロー
    ///     Connect() 接続
    ///         ├─ OnStoreConnected -> 接続完了
    ///         │   ├─ FetchProducts() 目録取得
    ///         │   │   ├─ OnProductsFetched -> 目録取得完了
    ///         │   │   │   └─ CheckEntitlement() 所有確認
    ///         │   │   │       └─ OnCheckEntitlement -> 所有状況
    ///         │   │   └─ OnProductsFetchFailed -> 目録取得失敗
    ///         │   └─ FetchPurchases() 注文取得
    ///         │       ├─ OnPurchasesFetched -> 注文取得完了(初期化完了)
    ///         │       └─ OnPurchasesFetchFailed -> 注文取得失敗
    ///         └─ OnStoreDisconnected -> 接続失敗
    /// 購入フロー
    ///     PurchaseProduct() 発注
    ///         ├─ OnPurchaseDeferred -> 承認待機(アプリへ制御を戻す、時間差でOnPurchasePendingが呼ばれる)
    ///         ├─ OnPurchasePending -> 権利付与 (消費財ならアプリへ制御を戻す)
    ///         │   └─ ConfirmPurchase() 確定 (消耗品の消費)
    ///         │       └─ OnPurchaseConfirmed 結果
    ///         │           ├─ ConfirmedOrder -> 購入完了(成功)
    ///         │           └─ FailedOrder -> 確定失敗
    ///         └─ OnPurchaseFailed -> 発注失敗
    /// </remarks>
    public class Purchaser {
		#region Static
		/// <summary>シングルトン</summary>
		private static Purchaser instance = new ();

        /// <summary>所有目録 製品の課金状況一覧、消費タイプは未消費を表す</summary>
        public static Inventory Inventory { get; private set; } = new ();

        /// <summary>有効 初期化が完了している</summary>
        public static bool IsValid => Status == PurchaseStatus.AVAILABLE;

		/// <summary>初期化状況</summary>
		public static PurchaseStatus Status { get; private set; } = PurchaseStatus.NOTINIT;

        /// <summary>製品定義</summary>
        private static List<ProductDefinition> ProductDefinitions { get; set; } = new ();

        /// <summary>ストアから得た製品目録</summary>
        public static List<Product> Products { get; private set; } = new ();

        /// <summary>処理中</summary>
        public static bool IsPurchasing => IsValid && instance.isPurchasing;

        /// <summary>IDから製品を得る</summary>
        /// <param name="productID">製品ID</param>
        /// <returns>製品</returns>
        public static Product? Product (string productID) => !string.IsNullOrEmpty (productID) && IsValid ? Products.FirstOrDefault (x => x.definition.id == productID) : null;

        /// <summary>購入の結果</summary>
        public static PurchaseResult Result { get; private set; } = new ();

        /// <summary>初期化通過時の処理 (複数の実行機会)</summary>
        //private static Action<bool>? onInitialized;

        /// <summary>クラスを初期化を開始してコールバックを得る (ノンブロック)</summary>
        /// <param name="products">製品定義</param>
        /// <param name="onInitialized">完了時コールバックハンドラ</param>
        public static async void Initialize (IEnumerable<ProductDefinition> products, Action<bool> onInitialized) {
            await InitializeAsync (products, onInitialized);
        }

        /// <summary>初期化完了通知</summary>
        private static Action<bool>? OnInitialized { get; set; }

        /// <summary>クラスを初期化して結果を得る</summary>
        /// <remarks>
        /// クラスはシングルトンとして振る舞います。<br/>
        /// 既に初期化済みの場合に製品定義を再度渡すと再初期化を試行します。<br/>
        /// 初回初期化時にオフラインだった場合は、オンラインになってから引数なしで呼ぶことで初期化を完了できます。
        /// (製品定義や完了時コールバックハンドラの上書きはできません。)<br/>
        /// `Status != PurchaseStatus.NOTINIT`の場合にコールバックがあります。<br/>
        /// </remarks>
        /// <param name="products">製品定義</param>
        /// <param name="onInitialized">完了時コールバックハンドラ</param>
        /// <returns>成否</returns>
        public static async Task<bool> InitializeAsync (IEnumerable<ProductDefinition>? products = null, Action<bool>? onInitialized = null) {
            Debug.Log ($"初期化({Status}): [{products?.Count () ?? 0}] {onInitialized is not null}");
            if (products is not null && IsValid) {
                // 再初期化要求
                Status = PurchaseStatus.NOTINIT;
            }
            products ??= ProductDefinitions ?? new ();
            ProductDefinitions = products.ToList ();
            onInitialized ??= OnInitialized;
            OnInitialized = onInitialized;
            // IAPを初期化する
            if (IsValid) {
                Debug.LogWarning ("既に初期化済み");
                onInitialized?.Invoke (true);
            } else if (ProductDefinitions.Count < 1) {
                Debug.LogWarning ("製品定義が空");
                Status = PurchaseStatus.UNAVAILABLE;
                onInitialized?.Invoke (false);
            } else if (!Tetr4labUtility.IsNetworkAvailable) {
                Debug.LogWarning ("インターネットに接続していない");
                Status = PurchaseStatus.OFFLINE;
                onInitialized?.Invoke (false);
            } else {
                try {
                    // Servicesを初期化
                    var options = new InitializationOptions ().SetEnvironmentName ("production");
                    await UnityServices.InitializeAsync (options);
                    // IAPを初期化
                    await instance.ConnectAsync ();
                    onInitialized?.Invoke (IsValid);
                }
                catch (Exception exception) {
					Debug.LogError ($"An error occurred during services initialization. {exception.Message}\n{exception.StackTrace}");
					Status = PurchaseStatus.UNAVAILABLE;
					onInitialized?.Invoke (false);
					return false;
				}
            }
			return IsValid;
		}

		/// <summary>復元 課金情報の復元を行い、失敗を含めて結果のコールバックを得る</summary>
		public static async Task<bool> RestoreAsync (Action<bool, string?>? onRestored = null) {
			if (IsValid) {
                if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.tvOS) {
                    instance.RestoreTransactions ();
                    await TaskEx.DelayWhile (() => IsPurchasing); // 処理完了を待つ
                } else {
                    instance.RestrationResult = true;
                }
                await instance.FetchProducts ();
                await instance.FetchPurchases ();
                onRestored?.Invoke (instance.RestrationResult, instance.RestrationError);
                return instance.RestrationResult;
            } else {
                onRestored?.Invoke (false, $"不適合: {Application.platform}");
                return false;
			}
		}

        /// <summary>所有状態</summary>
        public static EntitlementStatus CheckEntitlement (string productId)
            => IsValid 
                ? IsConsumable (productId)
                    ? EntitlementStatus.EntitledUntilConsumed // 未消費
                    : IsDeferred (productId) 
                        ? EntitlementStatus.EntitledButNotFinished // 承認待ち
                        : Inventory [productId] // 所持/不所持
                : EntitlementStatus.Unknown // 不明
            ;

        /// <summary>所有/未消費/未承認の不在</summary>
        public static bool IsPurchasable (string productId) => !IsStocked (productId) && !IsDeferred (productId);

        /// <summary>未承認</summary>
        public static bool IsDeferred (string productId) => IsValid && instance.DeferredOrder.ContainsKey (productId);

        /// <summary>未消費</summary>
        public static bool IsConsumable (string productId) => IsValid && instance.PendingOrder.ContainsKey (productId);

        /// <summary>所有/未消費</summary>
        public static bool IsStocked (string productId) => IsConsumable (productId) || IsValid && Inventory.Contains (productId);

        /// <summary>所有/未消費</summary>
        public static bool IsStocked (Product product) => IsStocked (product.definition.id);

        /// <summary>発注</summary>
        /// <remarks>消耗品は事後に別途消費する</remarks>
        /// <param name="product">製品</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        /// <returns>成否</returns>
        public static Task<bool> PurchaseAsync (Product product, Action<bool>? onPurchased = null)
            => PurchaseAsync (product.definition.id, onPurchased);

        /// <summary>発注</summary>
        /// <remarks>消耗品は事後に別途消費する</remarks>
        /// <param name="productId">製品ID</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        /// <returns>成否</returns>
        public static async Task<bool> PurchaseAsync (string productId, Action<bool>? onPurchased = null) {
			Debug.Log ($"PurchaseAsync ({productId})");
            if (!IsValid) {
                // 初期化できていないので再初期化
                await InitializeAsync ();
            }
            if (IsValid) {
                if (instance.Purchase (productId)) {
                    await TaskEx.DelayWhile (() => IsPurchasing); // 購入処理完了(保留を含む)を待つ
                    var success = Result.IsSuccess;
                    onPurchased?.Invoke (success);
                    return success;
                }
            }
            return false;
        }

        /// <summary>保留中の発注済み消費財を消費</summary>
        /// <param name="product">製品</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        /// <returns>成否</returns>
        public static Task<bool> ConfirmPurchaseAsync (Product product, Action<bool>? onPurchased = null)
            => ConfirmPurchaseAsync (product.definition.id, onPurchased);

        /// <summary>保留中の発注済み消費財を消費</summary>
        /// <param name="productId">製品ID</param>
        /// <param name="onPurchased">完了時コールバックハンドラ</param>
        /// <returns>成否</returns>
        public static async Task<bool> ConfirmPurchaseAsync (string productId, Action<bool>? onPurchased = null) {
            if (IsValid && await instance.ConfirmPurchase (productId)) {
                var success = Result == PurchaseResult.SUCCESS;
                onPurchased?.Invoke (success);
                return success;
            }
            return false;
        }

        #endregion

        /// <summary>コントローラー</summary>
        private StoreController controller;
        
        /// <summary>インスタンスが処理中(排他制御)</summary>
        private bool isPurchasing;

        /// <summary>消費の保留中の注文</summary>
        private Dictionary<string, PendingOrder> PendingOrder { get; set; } = new ();

        /// <summary>承認待機中の注文</summary>
        private Dictionary<string, DeferredOrder> DeferredOrder { get; set; } = new ();

        /// <summary>ストアから取得した確定済み注文</summary>
        private IReadOnlyList<ConfirmedOrder>? ConfirmedOrders { get; set; }

        /// <summary>コンストラクタ</summary>
        private Purchaser () {
            Debug.Log ("生成");
            // コントローラを取得
            controller = UnityIAPServices.StoreController ();
            // 購入開始イベント
            controller.OnPurchasePending += OnPurchasePending;
            // 購入完了イベント
            controller.OnPurchaseConfirmed += OnPurchaseConfirmed;
            // 購入失敗イベント
            controller.OnPurchaseFailed += OnPurchaseFailed;
            // 購入延期イベント
            controller.OnPurchaseDeferred += OnPurchaseDeferred;
            // 接続イベント
            controller.OnStoreConnected += OnStoreConnected;
            // 切断イベント
            controller.OnStoreDisconnected += OnStoreDisconnected;
            // 目録取得イベント
            controller.OnProductsFetched += OnProductsFetched;
            // 目録取得失敗イベント
            controller.OnProductsFetchFailed += OnProductsFetchFailed;
            // 製品所有確認イベント
            controller.OnCheckEntitlement += OnCheckEntitlement;
            // 購入状況取得イベント
            controller.OnPurchasesFetched += OnPurchasesFetched;
            // 購入状況取得失敗イベント
            controller.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        }

        /// <summary>ストアに接続</summary>
        async Task ConnectAsync () {
            Debug.Log ("初期化開始");
            await controller.Connect ();
            // 完了を待機
            await TaskEx.DelayWhile (() => Status == PurchaseStatus.NOTINIT || Status == PurchaseStatus.OFFLINE);
            Debug.Log ($"初期化完了 {Status}");
        }

        /// <summary>ストアに接続した</summary>
        async void OnStoreConnected () {
            Debug.Log ("接続");
            // 製品一覧を取得
            if (await FetchProducts ()) {
                // 注文状況の取得へ
                Status = await FetchPurchases () ? PurchaseStatus.AVAILABLE : PurchaseStatus.UNAVAILABLE;
            } else {
                // 取得に失敗したため初期化失敗
                Status = PurchaseStatus.UNAVAILABLE;
            }
        }

        /// <summary>ストアから切断された</summary>
        /// <param name="description">切断状況</param>
        void OnStoreDisconnected (StoreConnectionFailureDescription description) {
            Debug.Log ($"切断: {description.Message}");
            Status = PurchaseStatus.OFFLINE;
        }

        /// <summary>目録を取得中</summary>
        bool isFetchingProducts;

        /// <summary>目録を取得</summary>
        /// <returns>成否</returns>
        private async Task<bool> FetchProducts () {
            if (!isFetchingProducts) {
                Debug.Log ("目録請求");
                isFetchingProducts = true;
                Products.Clear ();
                Inventory.Clear ();
                controller.FetchProducts (ProductDefinitions);
            }
            await TaskEx.DelayWhile (() => isFetchingProducts);
            return Products.Count > 0;
        }

        /// <summary>目録を取得した</summary>
        /// <param name="products">製品</param>
        async void OnProductsFetched (List<Product> products) {
            Products = products;
            await UpdateInventory (products);
            Debug.Log ($"目録:\n{string.Join ('\n', products.ConvertAll (x => $"製品: {x.definition.id} / {x.definition.type}"))}");
            isFetchingProducts = false;
        }

        /// <summary>所有確認中の数</summary>
        int checkingEntitlementCount;

        /// <summary>所有状態の更新</summary>
        async Task UpdateInventory (IEnumerable<Product> products) {
            Debug.Log ("所有確認");
            foreach (Product product in products) {
                checkingEntitlementCount++;
                controller.CheckEntitlement (product);
            }
            // 確認完了待つ
            await TaskEx.DelayWhile (() => checkingEntitlementCount > 0);
        }

        /// <summary>所有確認</summary>
        /// <param name="entitlement">所有状態</param>
        void OnCheckEntitlement (Entitlement entitlement) {
            if (entitlement.Product is not null) {
                Inventory [entitlement.Product] = entitlement.Status;
                // 実消費の消耗品でも`EntitledUntilConsumed`にはならない様子
            }
            Debug.Log ($"所有: '{entitlement.Product?.definition.id}' {entitlement.Status}");
            checkingEntitlementCount--;
        }

        /// <summary>目録の取得に失敗した</summary>
        /// <param name="failed">失敗状況</param>
        void OnProductsFetchFailed (ProductFetchFailed failed) {
            Products = new ();
            Debug.Log ($"目録失敗: {failed.FailureReason}");
            isFetchingProducts = false;
        }

        /// <summary>注文状況を取得中</summary>
        bool isFetchingPurchases;

        /// <summary>注文状況を取得</summary>
        async Task<bool> FetchPurchases () {
            isFetchingPurchases = true;
            controller.FetchPurchases ();
            await TaskEx.DelayWhile (() => isFetchingPurchases);
            return ConfirmedOrders is not null;
        }

        /// <summary>注文状況取得</summary>
        /// <param name="orders">注文</param>
        void OnPurchasesFetched (Orders orders) {
            Debug.Log ($"注文状況: c={orders.ConfirmedOrders.Count} / p={orders.PendingOrders.Count} / d={orders.DeferredOrders.Count}");
            PendingOrder.Clear ();
            foreach (var order in orders.PendingOrders) {
                var items = order.CartOrdered.Items ();
                if (items.Count > 0) {
                    PendingOrder [items [0].Product.definition.id] = order;
                }
            }
            DeferredOrder.Clear ();
            foreach (var order in orders.DeferredOrders) {
                var items = order.CartOrdered.Items ();
                if (items.Count > 0) {
                    DeferredOrder [items [0].Product.definition.id] = order;
                }
            }
            ConfirmedOrders = orders.ConfirmedOrders;
            isFetchingPurchases = false;
        }

        /// <summary>注文状況取得失敗</summary>
        /// <param name="description">失敗状況</param>
        void OnPurchasesFetchFailed (PurchasesFetchFailureDescription description) {
            Debug.Log ($"注文状況取得失敗: {description.Message} {description.FailureReason}");
            ConfirmedOrders = null;
            isFetchingPurchases = false;
        }

        /// <summary>注文受諾(付与請求)</summary>
        /// <param name="order">保留中の注文</param>
        void OnPurchasePending (PendingOrder order) {
            Debug.Log ($"注文受諾: {order.Info.Receipt}");
            var consumable = false;
            var products = new List<Product> ();
            string? firstProductId = null;
            foreach (var item in order.CartOrdered.Items ()) {
                // 検証を行い、ユーザーにアイテムや権利を付与する
                Result = PurchaseResult.SUCCESS;
                firstProductId ??= item.Product.definition.id;
                Debug.Log ($"注文品: {item.Product.definition.id} {item.Product.definition.type}");
                if (item.Product.definition.type == ProductType.Consumable) {
                    consumable = true;
                }
                products.Add (item.Product);
            }
            // 承認待ちから抹消
            DeferredOrder.Remove (firstProductId!);
            if (consumable) {
                // 消耗品なら消費を保留して終了
                PendingOrder [firstProductId!] = order;
                Result = PurchaseResult.Pending;
                isPurchasing = false;
            } else {
                // ストア側に「購入処理の完了」を通知
                controller.ConfirmPurchase (order);
            }
        }

        /// <summary>保留中の注文を完了(消耗品の消費)</summary>
        /// <returns>保留中の注文の有無</returns>
        public async Task<bool> ConfirmPurchase (string productId) {
            Debug.Log ($"消費: {productId}");
            if (PendingOrder.ContainsKey (productId)) {
                Result = PurchaseResult.Confirming;
                // ストア側に「購入処理の完了」を通知
                controller.ConfirmPurchase (PendingOrder [productId]);
                await TaskEx.DelayWhile (() => Result == PurchaseResult.Confirming); // 確認完了を待つ
                PendingOrder.Remove (productId);
                return true;
            }
            Debug.Log ("該当の注文が見つからない");
            Result = PurchaseResult.NotFound;
            return false;
        }

        /// <summary>購入完了</summary>
        /// <param name="order">注文</param>
        async void OnPurchaseConfirmed (Order order) {
            var products = order.CartOrdered.Items ().ToList ().ConvertAll (x => x.Product);
            var ids = string.Join (", ", products.ConvertAll (x => $"{x.definition?.id}"));
            switch (order) {
                case FailedOrder failedOrder:
                    // 失敗の派生クラス
                    Debug.Log ($"購入完了失敗: Receipt={order.Info.Receipt} Items={ids}, Reason={failedOrder.FailureReason}, Order={failedOrder.Details}");
                    if (failedOrder.FailureReason == PurchaseFailureReason.Unknown && failedOrder.Details.Contains ("not found")) {
                        Result = PurchaseResult.NotFound;
                    } else {
                        Result = failedOrder.FailureReason;
                    }
                    break;
                case ConfirmedOrder confirmedOrder:
                    // 完了の派生クラス
                    await UpdateInventory (products);
                    Debug.Log ($"購入完了:  {order.Info.Receipt} {ids}");
                    Result = PurchaseResult.SUCCESS;
                    break;
                default:
                    Debug.Log ($"不明な購入完了結果: {order.Info.Receipt} {order.GetType ()}");
                    Result = PurchaseResult.ERROR;
                    break;
            }
            isPurchasing = false;
        }

        /// <summary>購入失敗</summary>
        /// <param name="order">失敗した注文</param>
        void OnPurchaseFailed (FailedOrder order) {
            Debug.Log ($"購入失敗: {order.FailureReason} / {order.Info.Receipt} {order.Details}");
            Result = order.FailureReason;
            isPurchasing = false;
        }

        /// <summary>承認待機</summary>
        /// <param name="order">承認の必要な注文</param>
        void OnPurchaseDeferred (DeferredOrder order) {
            Debug.Log ($"承認待機: {order.Info.Receipt}");
            var firstProductId = order.CartOrdered.Items ().FirstOrDefault ()?.Product.definition.id;
            DeferredOrder [firstProductId!] = order;
            Result = PurchaseResult.Deferring;
            isPurchasing = false;
        }

        /// <summary>発注</summary>
        /// <param name="productId">対象製品ID</param>
        private bool Purchase (string productId) {
            Debug.Log ($"発注: {productId}");
            if (PurchaseAvailable) {
                var product = Products.Find (x => x.definition.id == productId);
                if (instance.PendingOrder.ContainsKey (productId) == true) {
                    Debug.Log ("保留中");
                    Result = PurchaseFailureReason.ExistingPurchasePending;
                } else if (Inventory.Contains (productId)) {
                    Debug.Log ("所有中");
                    Result = PurchaseFailureReason.DuplicateTransaction;
                } else if (product?.IsValid () == true) {
                    isPurchasing = true;
                    controller.PurchaseProduct (productId);
                    Result = PurchaseResult.Purchasing;
                    return true;
                } else {
                    Debug.Log ($"無効な製品: {productId} {product?.definition.enabled} {product?.availableToPurchase}");
                    Result = PurchaseFailureReason.ProductUnavailable;
                }
            }
            return false;
        }

        /// <summary>発注</summary>
        /// <param name="product">対象製品</param>
        private bool Purchase (Product product) {
            Debug.Log ($"発注: {product.definition.id}");
            if (PurchaseAvailable) {
                if (instance.PendingOrder.ContainsKey (product.definition.id) == true) {
                    Debug.Log ("保留中");
                    Result = PurchaseFailureReason.ExistingPurchasePending;
                } else if (Inventory.Contains (product.definition.id)) {
                    Debug.Log ("所有中");
                    Result = PurchaseFailureReason.DuplicateTransaction;
                } else if (product.IsValid ()) {
                    isPurchasing = true;
                    controller.PurchaseProduct (product);
                    Result = PurchaseResult.Purchasing;
                    return true;
                } else {
                    Debug.Log ($"無効な製品: {product.definition.id} {product.definition.enabled} {product.availableToPurchase}");
                    Result = PurchaseFailureReason.ProductUnavailable;
                }
            }
            return false;
        }

        /// <summary>発注可能</summary>
        private bool PurchaseAvailable {
            get {
                if (!Tetr4labUtility.IsNetworkAvailable) {
                    Debug.Log ("インターネットへの接続経路がない");
                    Result = PurchaseResult.Disconnected;
                } else if (!IsValid) {
                    Debug.Log ("未初期化");
                    Result = PurchaseResult.Unavailable;
                } else if (isPurchasing) {
                    Debug.Log ("購入中");
                    Result = PurchaseResult.Busy;
                } else {
                    return true;
                }
                return false;
            }
        }

        /// <summary>復元の成否</summary>
        bool RestrationResult;

        /// <summary>復元エラー</summary>
        string? RestrationError;

        /// <summary>復元</summary>
        /// <remarks>呼び出し側でプラットフォームや初期化状況の確認が必要</remarks>
        /// <param name="onRestored">完了通知(成否を問わず)</param>
        private void RestoreTransactions () {
            isPurchasing = true;
            RestrationResult = false;
            RestrationError = null;
            controller.RestoreTransactions (OnTransactionsRestored);
        }

        /// <summary>復元完了</summary>
        /// <param name="success">成否</param>
        /// <param name="error">エラーメッセージ</param>
        void OnTransactionsRestored (bool success, string? error) {
            Debug.Log (success ? "復元成功" : $"復元失敗: {error}");
            RestrationResult = success;
            RestrationError = error;
            isPurchasing = false;
        }
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
				//$"transactionID={product.transactionID}",
				//$"Receipt has={product.hasReceipt}",
				$"Purchaser.Valid={Purchaser.IsValid}",
				//$"Receipt validation={Purchaser.ValidateReceipt (product)}",
				$"Possession={Purchaser.Inventory [product]}",
			});
		}

		/// <summary>有効性 製品がストアに登録されていることを示すが、ストアで有効かどうかには拠らない</summary>
		public static bool IsValid (this Product product) => product.definition.enabled && product.availableToPurchase;

		/// <summary>アプリ名を含まないタイトル</summary>
		public static string shortTitle (this ProductMetadata metadata)
            => (metadata != null && !string.IsNullOrEmpty (metadata.localizedTitle)) ? (new Regex (@"\s*\(.+\)$")).Replace (metadata.localizedTitle, "") : string.Empty;

	}	// ProductExtentions

	/// <summary>productID基準でProductの所有を表現する辞書</summary>
	public class Inventory : Dictionary<string, EntitlementStatus> {

        /// <summary>Productによるアクセス</summary>
        public new EntitlementStatus this [string productId] {
            get => ContainsKey (productId) ? base [productId] : EntitlementStatus.Unknown;
            set => base [productId] = value;
        }

        /// <summary>Productによるアクセス</summary>
        public EntitlementStatus this [Product product] {
			get => ContainsKey (product.definition.id) ? base [product.definition.id] : EntitlementStatus.Unknown;
			set => base [product.definition.id] = value;
		}

		/// <summary>Productによる存在確認</summary>
		public bool ContainsKey (Product product) => ContainsKey (product.definition.id);

        /// <summary>所有</summary>
        /// <param name="productId">製品ID</param>
        /// <returns>有無</returns>
        public bool Contains (string productId) => this [productId] == EntitlementStatus.FullyEntitled;

        /// <summary>所有</summary>
        /// <param name="product">製品</param>
        /// <returns>有無</returns>
        public bool Contains (Product product) => Contains (product.definition.id);

    }	// Inventory

}	// namespace
#endif