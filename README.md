---
title: Unity IAPを試してみた (Yet Another Purchaser.cs)
tags: Unity UnityIAP Android iOS C#
---
# 前提
- Unity & Purchasing
  - Unity 6000.0.79f1
  - com.unity.purchasing 5.3.1
    - IAP 4.x には対応しません。
    - 5.4.0, 5.4.1 ではビルドできませんでした。
- Apple App Store、Google Play Store アカウント
  - 2026/07/17時点では、Androidでのみ確認
- この記事では、Unity IAPの一部機能を限定的に使用し、汎用性のない部分があります。
  - サーバレス、消費／非消費タイプ使用、購読タイプ未使用
- この記事のソースは、実際のストアでテストしていますが、製品版での使用実績はありません。
  - ソース中のIDは実際と異なり、そのまま使用できるものではありません。

## 公式ドキュメント
- マニュアル
  - [Unity IAP](https://docs.unity3d.com/ja/current/Manual/UnityIAP.html)
  - [In App Purchasing 5.3](https://docs.unity3d.com/Packages/com.unity.purchasing@5.3/manual/index.html)
- スクリプトリファレンス
  - [In App Purchasing 5.3](https://docs.unity3d.com/Packages/com.unity.purchasing@5.3/api/UnityEngine.Purchasing.UnityPurchasing.html)

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
- パッケージ「In App Purchasing (`com.unity.purchasing`)」が必要です。
  - あらかじめ、パッケージマネージャで導入してください。
- プロジェクトにサービスを導入します。
  - `Project Settings` > `Services` > `In-App Purchasing` を「ON」にします。
  - さらに必要に応じて設定してください。
- その他、適切にストア毎の設定を行います。

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

- これらはストア・コンソールでの設定と過不足なく呼応している必要があります。
  - 過不足があると目録の取得ができずに初期化に失敗します。
  - Apple App Storeでは、IDと製品タイプの双方が設定されます。
    - 消費と非消費(恒久)は明確に区別されます。
  - Google Play Storeでは、IDが設定されますが、消費の有無についての設定はありません。
    - ストア側には消費と非消費(恒久)といった種別がなく、アプリ側で製品定義して消費を申告することで区別されています。
- ここでは、消費タイプ`Consumable`と非消費タイプ`NonConsumable`だけを扱い、購読タイプは扱いません。

### その他
詳しくは、ソースを参照してください。
