//	Copyright© tetr4lab.

using System;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
//using UnityEngine.AddressableAssets;

namespace Tetr4lab {

	/// <summary>タスク拡張</summary>
	public static class TaskEx {

		/// <summary>休止間隔</summary>
		private const int Tick = 16;

		/// <summary>1フレーム待機</summary>
		public static Task DelayOneFrame => Task.Delay (Tick);

		/// <summary>条件が成立する間待機</summary>
		/// <param name="predicate">条件</param>
		/// <param name="limit">msec単位の制限</param>
		/// <param name="tick">刻み</param>
		public static async Task DelayWhile (Func<bool> predicate, int limit = 0, int tick = 0) {
			tick = (tick > 0) ? tick : Tick;
			if (limit <= 0) {
				while (predicate ()) {
					await Task.Delay (tick);
				}
			} else {
				limit /= tick;
				while (predicate () && limit-- > 0) {
					await Task.Delay (tick);
				}
			}
		}

		/// <summary>条件が成立するまで待機</summary>
		/// <param name="predicate">条件</param>
		/// <param name="limit">msec単位の制限</param>
		/// <param name="tick">刻み</param>
		public static async Task DelayUntil (Func<bool> predicate, int limit = 0, int tick = 0) {
			tick = (tick > 0) ? tick : Tick;
			if (limit <= 0) {
				while (!predicate ()) {
					await Task.Delay (tick);
				}
			} else {
				limit /= tick;
				while (!predicate () && limit-- > 0) {
					await Task.Delay (tick);
				}
			}
		}

	}

	/// <summary>汎用ユーティリティ</summary>
	public static class Tetr4labUtility {

		/// <summary>ストリーミングアセットからデータを読み込んで返す</summary>
		public static byte [] LoadStreamingData (this string filename) {
			string sourcePath = Path.Combine (Application.streamingAssetsPath, filename);
			var gz = filename.EndsWith (".gz");
			if (sourcePath.Contains ("://")) { // Android
				using (var www = UnityWebRequest.Get (gz ? sourcePath.Substring (0, sourcePath.Length - 3) : sourcePath)) {
					www.SendWebRequest ();
					while (www.result == UnityWebRequest.Result.InProgress) { }
					if (www.result == UnityWebRequest.Result.Success) {
						return www.downloadHandler.data;
					}
				}
			} else if (File.Exists (sourcePath)) { // Mac, Windows, iPhone
				if (gz) {
					using (var data = File.OpenRead (sourcePath))
					using (var compresed = new GZipStream (data, CompressionMode.Decompress))
					using (var decompressed = new MemoryStream ()) {
						compresed.CopyTo (decompressed);
						return decompressed.ToArray ();
					}
				} else {
					return File.ReadAllBytes (sourcePath);
				}
			}
			return null;
		}

		/// <summary>ストリーミングアセットからテキストを読み込んで返す</summary>
		public static string LoadStreamingText (this string filename) {
			string sourcePath = Path.Combine (Application.streamingAssetsPath, filename);
			var gz = filename.EndsWith (".gz");
			if (sourcePath.Contains ("://")) { // Android
				using (var www = UnityWebRequest.Get (gz ? sourcePath.Substring (0, sourcePath.Length - 3) : sourcePath)) {
					www.SendWebRequest ();
					while (www.result == UnityWebRequest.Result.InProgress) { }
					if (www.result == UnityWebRequest.Result.Success) {
						return www.downloadHandler.text;
					}
				}
			} else if (File.Exists (sourcePath)) { // Mac, Windows, iPhone
				if (gz) {
					using (var data = File.OpenRead (sourcePath))
					using (var compresed = new GZipStream (data, CompressionMode.Decompress))
					using (var text = new MemoryStream ()) {
						compresed.CopyTo (text);
						return Encoding.UTF8.GetString (text.ToArray ());
					}
				} else {
					return File.ReadAllText (sourcePath);
				}
			}
			return null;
		}

		/// <summary>バイト列をBase64urlに変換</summary>
		public static string ToBase64url (this byte [] buffer) => Convert.ToBase64String (buffer).Trim ('=').Replace ("+", "-").Replace ("/", "_");

		/// <summary>文字列をBase64urlに変換</summary>
		public static string ToBase64url (this string target) => Convert.ToBase64String (Encoding.UTF8.GetBytes (target)).Trim ('=').Replace ("+", "-").Replace ("/", "_");

		/// <summary>設定上のネット接続の有効性 (実際に接続できるかどうかは別)</summary>
		public static bool IsNetworkAvailable => (Application.internetReachability != NetworkReachability.NotReachable);

		/// <summary>Vector3を範囲内に整合する</summary>
		public static Vector3 Clamp (this Vector3 vector, Vector3 minVector, Vector3 maxVector) {
			return new Vector3 (Mathf.Clamp (vector.x, minVector.x, maxVector.x), Mathf.Clamp (vector.y, minVector.y, maxVector.y), Mathf.Clamp (vector.z, minVector.z, maxVector.z));
		}

		/// <summary>Vector2を範囲内に整合する</summary>
		public static Vector2 Clamp (this Vector2 vector, Vector2 minVector, Vector2 maxVector) {
			return new Vector2 (Mathf.Clamp (vector.x, minVector.x, maxVector.x), Mathf.Clamp (vector.y, minVector.y, maxVector.y));
		}

		/// <summary>ドット区切り数字列を比較する</summary>
		/// <param name="a">対象文字列A</param>
		/// <param name="b">対象文字列B</param>
		/// <param name="number">比較する列数 (0なら全て)</param>
		/// <returns>A-Bの符号 (1, 0, -1)</returns>
		public static int CompareVersionString (this string a, string b, int number = 0) {
			var aSeries = a.Split ('.');
			var bSeries = b.Split ('.');
			int aNum, bNum;
			number = (number > 0) ? Mathf.Min (number, aSeries.Length) : aSeries.Length;
			for (var i = 0; i < number; i++) {
				if (i >= bSeries.Length) { return 1; }
				aNum = 0; int.TryParse (aSeries [i], out aNum);
				bNum = 0; int.TryParse (bSeries [i], out bNum);
				if (aNum > bNum) { return 1; } else if (aNum < bNum) { return -1; }
			}
			return (aSeries.Length == bSeries.Length) ? 0 : -1;
		}

		/// <summary>二点の位置から閾値を超えた4方位を算出する</summary>
		public static Vector2Int GetDir (this Vector2 currentPosition, Vector2 lastPosition, float threshold, Vector2 dpi = default (Vector2)) {
			var delta = (currentPosition - lastPosition) / ((dpi == default (Vector2)) ? new Vector2 (Screen.dpi, Screen.dpi) : dpi);
			var adx = Mathf.Abs (delta.x);
			var ady = Mathf.Abs (delta.y);
			var dir = Vector2Int.zero;
			if (adx > threshold || ady > threshold) {
				dir = (adx > ady) ?
					((delta.x < 0) ? Vector2Int.left : Vector2Int.right) :
					((delta.y < 0) ? Vector2Int.down : Vector2Int.up);
			}
			return dir;
		}

		/// <summary>テキストファイルへ保存する</summary>
		public static void SaveText (string name, object obj) {
			SaveText (name, string.Join ("\n", obj.ToString ()));
		}

		/// <summary>テキストファイルへ保存する</summary>
		public static void SaveText<T> (string name, T textlist) where T : IEnumerable<string> {
			SaveText (name, string.Join ("\n", textlist));
		}

		/// <summary>テキストファイルへ保存する</summary>
		public static void SaveText (string name, string text) {
			var path = System.IO.Path.Combine (Application.persistentDataPath, name);
			System.IO.File.WriteAllBytes (path, System.Text.Encoding.UTF8.GetBytes (text));
		}

		/// <summary>文字列がUUIDか判定</summary>
		public static bool IsUuid (this string str) {
			return (!string.IsNullOrEmpty (str)) && (new Regex (@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")).IsMatch (str);
		}

		/// <summary>条件が真の間ブロックする (制限時間付き)</summary>
		public static void WaitForWhile (Func<bool> checker,  float limitTime = float.MaxValue) {
			var limit = Time.realtimeSinceStartup + limitTime;
			while (checker () && Time.realtimeSinceStartup < limit) { }
		}

		/// <summary>オブジェクトが最前かどうかを判定</summary>
		public static bool IsLastSibling (this GameObject child) {
			return (child.transform.GetSiblingIndex () == child.transform.parent.childCount - 1);
		}

		/// <summary>入れ替え</summary>
		public static void Swap<T> (ref T lhs, ref T rhs) {
			var temp = lhs;
			lhs = rhs;
			rhs = temp;
		}

		/// <summary>範囲内か返す</summary>
		public static bool InArea (this float p, float min, float max) {
			return (p >= min && p < max);
		}

		/// <summary>エリア内か返す</summary>
		public static bool InArea (this Vector2 pos, Rect area) {
			return (pos.x >= area.x && pos.x < area.x + area.width) && (pos.y >= area.y && pos.y < area.y + area.height);
		}

		/// <summary>エリア内か返す</summary>
		public static bool InArea (this Vector3 pos, Rect area) {
			return InArea ((Vector2) pos, area);
		}

		/// <summary>範囲からの変位を返す</summary>
		public static float OutArea (this float p, float min, float max) {
			return (p < min) ? (p - min) : (p > max) ? (p - max) : 0;
		}

		/// <summary>エリアからの変位を返す</summary>
		public static Vector2 OutArea (this Vector2 pos, Rect area) {
			return new Vector2 (
				(pos.x < area.x) ? (pos.x - area.x) : (pos.x > area.x + area.width) ? (pos.x - area.x - area.width) : 0f,
				(pos.y < area.y) ? (pos.y - area.y) : (pos.y > area.y + area.height) ? (pos.y - area.y - area.height) : 0f
			);
		}

		/// <summary>エリアからの変位を返す</summary>
		public static Vector2 OutArea (this Vector3 pos, Rect area) {
			return OutArea ((Vector2) pos, area);
		}

		/// <summary>データを16進ダンプする</summary>
		public static string Dump (this byte [] data, int width = 16, string separator = " ") {
			var str = new List<string> { };
			for (var i = 0; i < data.Length; i++) {
				str.Add ($"{((i <= 0) ? "" : ((width > 0 && i % width == 0) ? "\n" : separator))}{data [i].ToString ("X2")}");
			}
			return str.Join ("");
		}

		/// <summary>数値が指定範囲内なら真、ただし最大値を含まない</summary>
		public static bool IsRange (this int num, int ceil, int min = 0) {
			return (num >= min && num < ceil);
		}

		/// <summary>整数を範囲内に整合</summary>
		public static int Clamp (this int num, int max, int min = 0, bool loop = false) {
			if (min > max) {
				var tmp = min;
				min = max;
				max = tmp;
			}
			if (num < min) {
				return loop ? max : min;
			} else if (num > max) {
				return loop ? min : max;
			} else {
				return num;
			}

		}

		/// <summary>配列を文字列化して連結</summary>
		public static string Join<T> (this T [] array, string separator) {
			return string.Join (separator, Array.ConvertAll (array, v => v.ToString ()));
		}

		/// <summary>リストを文字列化して連結</summary>
		public static string Join<T> (this List<T> list, string separator) {
			return (list == null) ? string.Empty : string.Join (separator, list.ConvertAll (v => v.ToString ()));
		}

		/// <summary>二次元配列リニア化</summary>
		public static T [] Linear<T> (this T [,] array) {
			var newArray = new T [array.Length];
			var i = 0;
			foreach (var d in array) {
				newArray [i++] = d;
			}
			return newArray;
		}

		/// <summary>一次元配列マトリクス化</summary>
		public static T [,] Matrix<T> (this T [] array, int width, int height) {
			var newArray = new T [width, height];
			var i = 0;
			foreach (var d in array) {
				newArray [(i / height) % width, i % height] = d;
				i++;
			}
			return newArray;
		}

		/// <summary>配列スライス</summary>
		/// <typeparam name="T">配列の型</typeparam>
		/// <param name="array">配列</param>
		/// <param name="begin">開始インデックス</param>
		/// <param name="end">終了インデックス+1</param>
		/// <returns>新しい配列</returns>
		public static T [] Slice<T> (this T [] array, int begin = 0, int end = -1) {
			var len = array.Length;
			if (begin < 0) {
				begin = 0;
			}
			if (begin >= len) {
				begin = len - 1;
			}
			if (end < begin || end > len) {
				end = len;
			}
			var newArray = new T [end - begin];
			if (newArray.Length > 0) {
				System.Array.Copy (array, begin, newArray, 0, end - begin);
			}
			return newArray;
		}

		/// <summary>配列シフト (先頭から値を取り出して前へ詰め、最後に値を入れる)</summary>
		public static T Shift<T> (this T [] array, T value = default (T)) {
			var rc = value;
			if (array != null & array.Length > 0) {
				rc = array [0];
				if (array.Length > 1) {
					Array.Copy (array, 1, array, 0, array.Length - 1);
				}
				array [array.Length - 1] = value;
			}
			return rc;
		}

		/// <summary>配列逆シフト (最後から値を取り出して後へ詰め、先頭に値を入れる)</summary>
		public static T Unshift<T> (this T [] array, T value = default (T)) {
			var rc = value;
			if (array != null & array.Length > 0) {
				rc = array [array.Length - 1];
				if (array.Length > 1) {
					Array.Copy (array, 0, array, 1, array.Length - 1);
				}
				array [0] = value;
			}
			return rc;
		}

		/// <summary>Collection<T>がnullまたは空であれば真</summary>
		public static bool IsNullOrEmpty<T> (this ICollection<T> collection) {
			return (collection == null || collection.Count == 0);
		}

		/// <summary>T []がnullまたは空であれば真</summary>
		public static bool IsNullOrEmpty<T> (this T [] array) {
			return (array == null || array.Length == 0);
		}

		/// <summary>変位⇒方位番号 (drul エラーなら-1)</summary>
		public static int NumberOfDir (this Vector2Int dir) {
			return Array.IndexOf<Vector2Int> (dirs, dir);
		}
		private static readonly Vector2Int [] dirs = { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left };

		/// <summary>名前⇒変位 (エラーならゼロ)</summary>
		public static Vector2Int DirOfName (this string name) {
			var i = Array.IndexOf (DirNames, name);
			return i.IsRange (DirVectors.Length) ? DirVectors [i] : Vector2Int.zero;
		}
		public static readonly string [] DirNames = new [] { "down", "right", "up", "left", };
		public static readonly Vector2Int [] DirVectors = new [] { Vector2Int.down, Vector2Int.right, Vector2Int.up, Vector2Int.left, };

		/// <summary>単純ベクトル回転 (90度単位)</summary>
		public static Vector2Int Rotate (this Vector2Int vector, int digree) {
			switch ((((digree / 90) % 4) + 4) % 4) {
				case 1: // 90
					return new Vector2Int (vector.y, -vector.x);
				case 2: // 180
					return new Vector2Int (-vector.x, -vector.y);
				case 3: // 270
					return new Vector2Int (-vector.y, vector.x);
				default: // 0
					return vector;
			}
		}

		/// <summary>矩形内位置ベクトル回転 (90度単位) (サイズは回転前のものを与える)</summary>
		/// <param name="pos">矩形内の位置</param>
		/// <param name="size">回転前の矩形サイズ</param>
		/// <param name="digree">回転角</param>
		/// <returns>回転後の位置</returns>
		public static Vector2Int Roll (this Vector2Int pos, Vector2Int size, int digree) {
			switch ((((digree / 90) % 4) + 4) % 4) {
				case 1: // 90
					return new Vector2Int (pos.y, size.x - pos.x - 1);
				case 2: // 180
					return new Vector2Int (size.x - pos.x - 1, size.y - pos.y - 1);
				case 3: // 270
					return new Vector2Int (size.y - pos.y - 1, pos.x);
				default:// 0
					return new Vector2Int (pos.x, pos.y);
			}
		}

		/// <summary>単純ベクトル反転</summary>
		public static Vector2Int Flip (this Vector2Int vector, bool horizontal, bool vertical) {
			return new Vector2Int (horizontal ? -vector.x : vector.x, vertical ? -vector.y : vector.y);
		}

		/// <summary>矩形内位置ベクトル反転</summary>
		/// <param name="pos">矩形内の位置</param>
		/// <param name="size">矩形サイズ</param>
		/// <param name="horizontal">水平反転</param>
		/// <param name="vertical">垂直反転</param>
		/// <returns>反転後の位置</returns>
		public static Vector2Int Flip (this Vector2Int pos, Vector2Int size, bool horizontal, bool vertical) {
			return new Vector2Int (horizontal ? size.x - pos.x - 1 : pos.x, vertical ? size.y - pos.y - 1 : pos.y);
		}

	}

	/// <summary>デリゲート</summary>
#region Delegates
	public delegate IEnumerator CoRoutine ();
	public delegate IEnumerator CoRoutine<T> (T obj);
	public delegate IEnumerator CoRoutine<T0, T1> (T0 obj0, T1 obj1);
	public delegate IEnumerator CoRoutine<T0, T1, T2> (T0 obj0, T1 obj1, T2 obj2);
	public delegate T Parser<T> (string str);
#endregion

	/// <summary>独自のBase64</summary>
	public static class Nary64 {
		/// <summary>64進数表現文字</summary>
		public static readonly string NaryChr = @"[0-9A-Za-z_!]";
		/// <summary>64進数文字辞書 (64)</summary>
		public static readonly char [] Nary = new char [] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', '_', '!' };
		/// <summary>基数(64)</summary>
		private static readonly int nBase = Nary.Length;
		/// <summary>64進数化</summary>
		public static string ToNary (this int num) {
			var str = new List<char> { };
			do {
				str.Insert (0, Nary [num % nBase]);
				num /= nBase;
			} while (num != 0);
				return new string (str.ToArray ());
		}
		/// <summary>整数化</summary>
		public static int FromNary (this string narystr) {
			var chars = narystr.ToCharArray ();
			Array.Reverse (chars);
			var number = 0;
			foreach (char chr in narystr) {
				number = number * 64 + Array.IndexOf (Nary64.Nary, chr);
			}
			return number;
		}
	}

	/// <summary>真偽値ペア</summary>
	public struct Vector2Bool {

		public bool x;
		public bool y;

		public bool And => x & y;
		public bool Or => x | y;
		public bool Xor => x ^ y;

		public static bool operator true (Vector2Bool a) { return a.x & a.y; }
		public static bool operator false (Vector2Bool a) { return !a.x & !a.y; }
		public static Vector2Bool operator & (Vector2Bool a, Vector2Bool b) { return new Vector2Bool (a.x & b.x, a.y & b.y); }
		public static Vector2Bool operator | (Vector2Bool a, Vector2Bool b) { return new Vector2Bool (a.x | b.x, a.y | b.y); }
		public static Vector2Bool operator ^ (Vector2Bool a, Vector2Bool b) { return new Vector2Bool (a.x ^ b.x, a.y ^ b.y); }
		public static Vector2Bool operator ! (Vector2Bool a) { return new Vector2Bool (!a.x, !a.y); }
		public static bool operator == (Vector2Bool a, Vector2Bool b) { return a.x == b.x && a.y == b.y; }
		public static bool operator != (Vector2Bool a, Vector2Bool b) { return a.x != b.x || a.y != b.y; }

		public static readonly Vector2Bool True = new Vector2Bool (true, true);
		public static readonly Vector2Bool False = new Vector2Bool (false, false);
		public static readonly Vector2Bool XnotY = new Vector2Bool (true, false);
		public static readonly Vector2Bool YnotX = new Vector2Bool (false, true);

		public Vector2Bool (bool x, bool y) {
			this.x = x;
			this.y = y;
		}

		public override bool Equals (object obj) {
			return (obj != null && obj is Vector2Bool && x == ((Vector2Bool) obj).x && y == ((Vector2Bool) obj).y);
		}

		public override int GetHashCode () {
			return (x ? 1 : 0) + (y ? 2 : 0);
		}

		public override string ToString () {
			return $"({x}, {y})";
		}

	}

	/// <summary>論理値許容型SystemLanguage</summary>
	public struct Language : IEquatable<Language> {
		private bool hasValue;
		private SystemLanguage language;
		public static readonly Language Undef = new Language (false);
		public Language (bool _hasValue) {
			hasValue = _hasValue;
			language = _hasValue ? Application.systemLanguage : SystemLanguage.Unknown;
		}
		public Language (SystemLanguage _language) {
			hasValue = _language != SystemLanguage.Unknown;
			language = _language;
		}
		public static bool TryParse (string name, bool ignoreCase, out Language language) {
			if (string.IsNullOrEmpty (name) || !Enum.TryParse (name, ignoreCase, out SystemLanguage syslang)) {
				language = Undef;
				return false;
			}
			language = syslang;
			return true;
		}
		public static bool TryParse (string name, out Language language) => TryParse (name, false, out language);
		public static Language Parse (string name, bool ignoreCase) { TryParse (name, ignoreCase, out var language); return language; }
		public static Language Parse (string name) { TryParse (name, false, out var language); return language; }
		public SystemLanguage GetValueOrDefault () => hasValue ? language : SystemLanguage.Unknown;
		public SystemLanguage GetValueOrDefault (Language l) => hasValue ? language : (SystemLanguage) l;
		public static implicit operator bool (Language l) => l.hasValue;
		public static implicit operator SystemLanguage (Language l) => l.hasValue ? l.language : SystemLanguage.Unknown;
		public static implicit operator Language (bool b) => new Language (b);
		public static implicit operator Language (SystemLanguage l) => new Language (l);
		public override string ToString () => hasValue ? language.ToString () : "Undef";
		public static bool operator == (Language a, Language b) => (a.hasValue == b.hasValue) && (!a.hasValue || a.language == b.language);
		public static bool operator != (Language a, Language b) => !(a == b);
		public bool Equals (Language other) => (hasValue == other.hasValue) && (language == other.language);
		public override bool Equals (object obj) => (obj == null || GetType () != obj.GetType ()) ? false : Equals ((Language) obj);
		public override int GetHashCode () => hasValue ? language.GetHashCode () : int.MinValue;
	}

}
