---
title: Unity IAPを試してみた (Yet Another Purchaser.cs)
tags: Unity UnityIAP Android iOS C#
---
# 前提
- Unity & Purchasing
	- Unity 2022.3.51f1
	- com.unity.purchasing 4.12.2
	- 動作する組み合わせ
		- Unity 2021.3.23f1, com.unity.purchasing 4.7.0
		- Unity 2021.3.27f1, com.unity.purchasing 4.8.0
        - Unity 2021.3.32f1, com.unity.purchasing 4.9.4
        - Unity 2022.3.51f1, target SDK 35, com.unity.purchasing 4.12.2 (Billing Library 6)
- Apple App Store、Google Play Store アカウント
- この記事では、Unity IAPの一部機能を限定的に使用し、汎用性のない部分があります。
  - サーバレス、消費／非消費タイプ使用、購読タイプ未使用
- この記事のソースは、実際のストアでテストしていますが、製品版での使用実績はありません。
  - ソース中のIDは実際と異なり、そのまま使用できるものではありません。

## 公式ドキュメント
- マニュアル
  - [Unity IAP](https://docs.unity3d.com/ja/current/Manual/UnityIAP.html)
  - [In App Purchasing 4.9](https://docs.unity3d.com/Packages/com.unity.purchasing@4.9/manual/index.html)
- スクリプトリファレンス
  - [In App Purchasing 4.9](https://docs.unity3d.com/Packages/com.unity.purchasing@4.9/api/UnityEngine.Purchasing.UnityPurchasing.html)

# できること
- Unity In-App Purchasing をスクリプトから使います。
	- 購入、消費、リストア(iOS)
- スクリプトレスで使うのでしたら、より簡易な方法が用意されています。公式ドキュメントや他の記事を参照してください。

# 準備
- ストアの開発アカウントが必要です。
- アプリを登録してください。
- アプリ内購入アイテムを登録してください。

# 使い方

### 導入
- プロジェクトにサービスを導入します。
  - `Project Settings` > `Services` > `In-App Purchasing` で「ON」にします。
  - [Getting Started](https://docs.unity3d.com/Packages/com.unity.purchasing@4.3/manual/GettingStarted.html)
- メニュー`Services` > `In-App Purchasing` > `Receipt validation obfuscator...`で、レシート検証の(難読化された)コードを生成します。
  - ダイアログに表示される手順に従って処理を進めると、`Assets/Scripts/UnityPurchasing/generated/`にコードが生成されます。
  - [Receipt Obfuscation](https://docs.unity3d.com/Packages/com.unity.purchasing@4.3/manual/UnityIAPValidatingReceipts.html)

#### このライブラリ
- `Package Manager` > `Add package from git URL...` で以下を入力します。
```
https://github.com/tetr4lab/UnityIAP-Test.git?path=/Assets/UnityIAP
```
- 以下のネームスペースを使用します。
```csharp
using Tetr4lab.UnityEngine.InAppPuchaser;
```

#### 依存ライブラリ
- `Package Manager` > `Add package from git URL...` で、順に以下を入力します。
```
https://github.com/tetr4lab/Tetr4labNugetPackages.git?path=/Tetr4lab
```

```
https://github.com/tetr4lab/Tetr4labUnityUtilities.git?path=/Assets/Utilities
```

### ネームスペース
- `UnityEngine.Purchasing`
  - 必須のネームスペースです。
- `UnityEngine.Purchasing.Security`
  - レシートの検証で必要なネームスペースです。
- `UnityEngine.Purchasing.Default`
  - この記事では扱いません。
- `UnityEngine.Purchasing.Extension`
  - この記事では扱いません。
- `UnityEngine.Purchasing.MiniJSON`
  - この記事では扱いません。

### 製品定義
- 初期化のために製品定義が必要です。
- 製品定義は製品のIDとタイプのセットです。

```csharp:Sample.cs
var products = new [] {
	new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item1", ProductType.Consumable),
	new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item2", ProductType.NonConsumable),
	new ProductDefinition ("jp.nyanta.tetr4lab.unityiaptest.item3", ProductType.NonConsumable),
};
```

- これらはストア・コンソールでの設定と正しく呼応している必要があります。
  - Apple App Storeでは、IDと製品タイプの双方が設定されます。
    - 消費と非消費(恒久)は明確に区別されます。
  - Google Play Storeでは、IDが設定されますが、消費の有無についての設定はありません。
    - ストア側には消費と非消費(恒久)といった種別がなく、アプリ側で製品定義して消費を申告することで区別されています。
- ここでは、消費タイプ`Consumable`と非消費タイプ`NonConsumable`だけを扱い、購読タイプは扱いません。

### 初期化
- 製品定義を渡して初期化を行います。
  - `Purchaser.Initialize(products, null)`の形では、初期化を開始するのみでブロックされません。
    - `Purchaser.Initialize(products, success => Debug.Log (success))`とすると、結果のコールバックを受けることができます。
  - `var success = await Purchaser.InitializeAsync (products)`とすれば、同期的に成否を得られます。
- 例えば、以下のように書くことで、初期化の完了を待って課金アイテム一覧を表示するようなことことが可能です。
  - この`Purchaser.Products`は、ストアから得た製品目録`IStoreController.products`を参照します。

```csharp:Purchaser.cs
	private async Task CreateCatalog () {
		if (await Purchaser.InitializeAsync (products)) {
			Catalog.Create (CatalogHolder);
			foreach (var product in Purchaser.Products.all) {
				CatalogItem.Create (Catalog.ScrollRect.content, product);
			}
		}
	}
```

- 他に、以下のプロパティをポーリングすることも可能です。
  - `Purchaser.Valid`で初期化の完了を取得できます。
  - `Purchaser.Status`で初期化の状況を取得できます。

### 製品目録
- 初期化に成功すると、`Purchaser.Products.all`で、ストアから得た製品情報を参照できます。

```csharp:Sample.cs
foreach (var product in Purchaser.Products.all) {
	Debug.Log (product.GetProductProperties ());
}
```

- `Purchaser.Products.GetProduct (string id)`で、個別に製品情報を取得できます。
  - `id`が見つからない場合は`null`が返ります。
- `((Product) product).Valid ()`で、製品がストアに登録されているか確認できます。
- `((Product) product).metadata.shortTitle ()`で、アプリ名を含まない製品(アイテム)名を取得できます。

#### 目録の制約
- 初期化時に定義を渡さなかった製品は、ストアにあっても製品目録には載りません。
  - ストアから製品のカタログが得られるわけではありません。つまり、ストアに新製品を登録しただけでは、製品に組み込むことはできません。
- `Product.definition.enabled`は、ストアのコンソールで設定されている有効/無効状態に関わりなく常に`true`になります。
- `Product.availableToPurchase`は、ストアにアイテムがない場合とストアで無効になっている場合に`False`になります。
- ストアでの製品の有効/無効は、アプリの使用する製品定義に連動させ、緊急時以外は、ストア独自に製品を無効化しないようにしてください。

### 所有状態
- 「非消費アイテムが購入済み」あるいは「消費アイテムが購入済みで未消費」であることを取得するには、`bool? Purchaser.IsStocked (Product product)`または`bool? Purchaser.IsStocked (string productID)`を使用します。
	- 指定した製品が無効だったり、`Purchaser`が未初期化の場合は`null`を返します。
- あるいは、`Purchaser.Inventory`を参照することもできます。
  - `Purchaser.Inventory`を書き換えて所有状態を変化させることはできません。外部から書き換えると不整合が生じます。
- `Purchaser.Inventory [Product product]`または`Purchaser.Inventory [string productID]`で、所有状態を取得できます。
  - 存在しない製品を指定すると例外が発生します。
- `Purchaser.Inventory.ContainsKey (Product product)`または`Purchaser.Inventory.ContainsKey (string productID)`で、製品の存在を確認できます。

### 購入
- `Purchaser.Purchase (Product product)`または`Purchaser.Purchase (string productID)`により、課金処理が開始されます。
  - `Purchaser.Purchase (Product product, Action<bool> onPurchased)`または`Purchaser.Purchase (string productID, Action<bool> onPurchased)`とすると、結果のコールバックを受けることができます。
- `await Purchaser.PurchaseAsync (Product product)`または`await Purchaser.PurchaseAsync (string sku)`により、同期的に成否を得られます。
- `Purchaser.PurchaseValid`をポーリングすることで、課金処理が進行中であることを確認できます。
  - ただし、課金処理を開始したものの、エラーのために進行中にならない場合もあります。
- `Purchaser.Result`により、直前の課金処理の失敗の理由を取得できます。

### 購入の完了
- 課金処理に成功すると、非消耗品では購入が完了し、消耗品では「購入済みで未消費(所有している)」状態になります。
  - 未消費状態は、アプリの中断時にも保持されます。
- `Purchaser.ConfirmPendingPurchase (Product product)`または`Purchaser.ConfirmPendingPurchase (string productID)`で消耗品を消費できます。
  - 消費に成功すると真が返されて「未所有」状態になります。

### 復元
- `Purchaser.Restore (Action<bool, string> onRestored = null)`で、課金情報の復元を行い、結果のコールバックを得ることができます。
  - Google Play Storeでは、何も処理されず、常に成功します。
