# EtherNet/IP コネクション設定 用語集(日本語表記付き)

EtherNet/IPはCIP(共通産業プロトコル)をイーサネット上に実装した産業用通信プロトコルです。ここではコネクション設定に関連する用語を、日本語名称とあわせて分類してまとめます。

---

## 1. 基本アーキテクチャ

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **CIP (Common Industrial Protocol)** | 共通産業プロトコル | EtherNet/IP、DeviceNet、ControlNetなどで共通に使われる上位通信プロトコル。オブジェクトモデルに基づく。 |
| **EtherNet/IP** | イーサネット・アイピー | CIPをTCP/IPおよびUDP/IP上に実装したネットワーク規格。ODVAが管理。 |
| **Originator** | 発信元/要求元 | コネクションの確立を要求する側の機器(通常はPLCやスキャナ)。 |
| **Target** | 対象/接続先 | コネクション要求を受け取る側の機器(通常はI/Oデバイスやアダプタ)。 |
| **Scanner** | スキャナ | Originator役の機器を指す呼び方(主にI/O通信の文脈)。 |
| **Adapter** | アダプタ | Target役の機器を指す呼び方(主にI/O通信の文脈)。 |

---

## 2. メッセージング方式

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **Implicit Messaging** | 暗黙的メッセージング | I/Oデータをリアルタイムで周期的にやり取りする通信。UDP/IPを使用。 |
| **Explicit Messaging** | 明示的メッセージング | パラメータ設定や診断情報の取得など、非周期的な要求/応答通信。TCP/IPを使用。 |
| **Class 1 Connection** | クラス1コネクション | Implicit MessagingによるI/Oデータ通信コネクション。 |
| **Class 3 Connection** | クラス3コネクション | Explicit Messagingによる接続指向の通信コネクション。 |
| **UCMM (Unconnected Message Manager)** | 非コネクション型メッセージ管理 | コネクションを確立せずに単発でExplicit Messageを送る仕組み。 |

---

## 3. コネクション確立に関する用語

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **Forward Open** | コネクション開設要求 | Originatorがコネクションを確立するために送るCIPサービス要求。 |
| **Forward Close** | コネクション終了要求 | 確立済みのコネクションを終了するためのサービス要求。 |
| **Connection ID (CID)** | コネクション識別子 | 各コネクションに割り当てられる識別子。O2T/T2Oそれぞれに個別に割り当てられる。 |
| **Connection Serial Number** | コネクション・シリアル番号 | Originatorが発行するコネクションの一意な番号。Forward Openのパラメータの一つ。 |
| **Originator Vendor ID / Serial Number** | 発信元ベンダーID/シリアル番号 | コネクション要求元機器のベンダーIDとシリアル番号。 |
| **Connection Path (EPATH)** | コネクション経路 | 接続先のオブジェクト(Assembly Instanceなど)を指定する経路情報。 |
| **Session Handle** | セッション識別子 | TCP接続確立後、Register Session要求により発行される識別子。Explicit Messagingで使用。 |
| **Register Session** | セッション登録 | Encapsulationプロトコルにおいて、TCPセッションを開始するためのコマンド。 |
| **Encapsulation Protocol** | カプセル化プロトコル | CIPメッセージをTCP/UDP上でカプセル化して送受信するためのプロトコル層。 |

---

## 4. コネクションパラメータ

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **RPI (Requested Packet Interval)** | 要求パケット間隔 | I/Oデータを送受信する周期(ミリ秒単位)。Originatorが要求し、Targetがサポート可能かを確認する。 |
| **Timeout Multiplier** | タイムアウト乗数 | コネクションタイムアウトを算出する際にRPIに掛ける係数。 |
| **Connection Timeout (Watchdog)** | コネクション監視時間 | 一定時間内にデータが届かない場合にコネクションを異常とみなす監視時間。 |
| **Trigger Type** | トリガー方式 | I/Oデータ送信のトリガー方式。以下の3種類が代表的。 |
| **Cyclic** | 周期送信 | 一定周期(RPI)で送信。 |
| **Change of State (COS)** | 状態変化時送信 | データに変化があった場合のみ送信。 |
| **Application Triggered** | アプリケーション主導送信 | アプリケーション側の要求により送信。 |
| **Transport Class Trigger** | トランスポートクラス・トリガー | コネクションの方向性、トリガー種別、Transport Classをまとめて指定するパラメータ。 |
| **Electronic Key** | 電子キー(機種照合) | 接続先デバイスのVendor ID、Product Type、Product Code、Major/Minor Revisionを照合し、誤接続を防ぐ仕組み。 |

---

## 5. データ方向・データ種別

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **O2T (Originator to Target)** | 発信元→対象方向 | Originator側からTarget側へ送るデータ方向。 |
| **T2O (Target to Originator)** | 対象→発信元方向 | Target側からOriginator側へ送るデータ方向。 |
| **Produced Data** | 送信データ | 自分から送信するデータ。 |
| **Consumed Data** | 受信データ | 相手から受信するデータ。 |
| **Configuration Data** | 設定データ | コネクション確立時にTargetへ送る初期設定用データ(オプション)。 |
| **Real Time Format / Run-Idle Header** | リアルタイム形式/ラン・アイドルヘッダ | I/Oデータの先頭に付与され、Originatorの動作状態(運転中/停止中)を示す4バイトのヘッダ。 |

---

## 6. 通信形態

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **Unicast Connection** | 単一宛先接続 | 1対1でデータを送信するコネクション形態。 |
| **Multicast Connection** | 複数宛先同時配信接続 | 1つのTargetから複数の受信先へ同時に配信するコネクション形態。主にT2O方向で使用。 |
| **Point-to-Point** | 一対一接続 | Unicast接続の別称として使われることがある。 |
| **Rack Optimization Connection** | ラック最適化コネクション | 同一シャーシ内の複数モジュールのI/Oデータをまとめて1つのコネクションで扱う最適化方式。 |
| **Direct Connection** | 個別接続 | 各モジュールごとに個別のコネクションを確立する方式。 |

---

## 7. 主要CIPオブジェクト(コネクション関連)

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **Identity Object** | アイデンティティ・オブジェクト | Vendor ID、Product Codeなど機器識別情報を保持するオブジェクト。 |
| **Assembly Object** | アセンブリ・オブジェクト | 複数のI/Oデータをひとまとめにして送受信するためのオブジェクト。 |
| **Connection Manager Object** | コネクション管理オブジェクト | Forward Open/Forward Closeを処理し、コネクションの生成・管理を行うオブジェクト。 |
| **TCP/IP Interface Object** | TCP/IPインタフェース・オブジェクト | IPアドレスなどTCP/IP設定を保持するオブジェクト。 |
| **Ethernet Link Object** | イーサネットリンク・オブジェクト | 物理層/リンク層(速度、Duplexなど)の情報を保持するオブジェクト。 |
| **Port Object** | ポート・オブジェクト | デバイスの通信ポート情報を管理するオブジェクト。 |

---

## 8. Predefined Connection Set(I/O通信の代表的なコネクション種別)

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **Exclusive Owner** | 排他制御接続 | 一台のOriginatorのみが排他的にTargetのI/Oデータを制御・取得するコネクション。 |
| **Input Only** | 入力専用接続 | 出力は行わず、入力データのみを受信するコネクション。複数Originatorから同時接続可能。 |
| **Listen Only** | 傍受専用接続 | 既に他のOriginatorが接続している状態で、出力は一切行わず、データを傍受するのみのコネクション。 |
| **Redundant Owner** | 冗長制御接続 | 冗長化構成で複数のOriginatorが同一Targetを制御できるようにするコネクション形態。 |

---

## 9. その他関連用語

| 英語用語 | 日本語名称 | 説明 |
|---|---|---|
| **EPATH** | イーパス(経路情報) | CIPオブジェクトへの経路(Class ID、Instance ID、Attribute IDなど)を表現するデータ形式。 |
| **Class ID** | クラスID | CIPオブジェクトの種類を表す識別子。 |
| **Instance ID** | インスタンスID | クラス内の個々のインスタンスを表す識別子。 |
| **Attribute ID** | 属性ID | オブジェクトが持つ個々の属性(パラメータ)を表す識別子。 |
| **Service Code** | サービスコード | Get_Attribute_Single、Set_Attribute_Single、Forward Openなど、実行する操作の種類を表すコード。 |
| **Network Connection ID (NCID)** | ネットワーク・コネクション識別子 | 実際のUDP/IPパケット上でコネクションを識別するために使われるID(CIDと対応)。 |
| **Vendor ID** | ベンダーID | 機器のメーカーを識別する番号。 |
| **Product Code** | 製品コード | 製品(型式)を識別する番号。 |
| **Product Type** | 製品種別 | 製品のカテゴリを表す番号(例:汎用I/O、AC駆動装置など)。 |

---

### 補足
- 実務上は特に **RPI(要求パケット間隔)**、**Timeout Multiplier(タイムアウト乗数)**、**Connection Path(コネクション経路)**、**Trigger Type(トリガー方式)**、**Unicast/Multicast(単一/複数宛先)**、**Exclusive Owner/Input Only/Listen Only(排他制御/入力専用/傍受専用)** の設定を、機器のEDSファイルやPLC設定画面上で調整することが多いです。
- 詳細な仕様はODVAが公開する「The CIP Networks Library」(Volume 1: Common Industrial Protocol、Volume 2: EtherNet/IP Adaptation)に準拠しています。
