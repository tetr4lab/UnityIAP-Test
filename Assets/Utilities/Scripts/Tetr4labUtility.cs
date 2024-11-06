using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Tetr4lab.Utilities {

	/// <summary>汎用ユーティリティ</summary>
	public static class Tetr4labUtility {

		/// <summary>ストリーミングアセットからデータを読み込んで返す</summary>
        /// <param name="filename"></param>
        /// <returns></returns>
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
        /// <param name="filename"></param>
        /// <returns></returns>
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

		/// <summary>設定上のネット接続の有効性 (実際に接続できるかどうかは別)</summary>
		public static bool IsNetworkAvailable => (Application.internetReachability != NetworkReachability.NotReachable);

		/// <summary>テキストファイルへ保存する</summary>
        /// <param name="name"></param>
        /// <param name="text"></param>
		public static void SaveTextFile (string name, string text) {
			var path = System.IO.Path.Combine (Application.persistentDataPath, name);
			System.IO.File.WriteAllBytes (path, System.Text.Encoding.UTF8.GetBytes (text));
		}

		/// <summary>オブジェクトが最前かどうかを判定</summary>
        /// <param name="child"></param>
        /// <returns></returns>
		public static bool IsLastSibling (this GameObject child) {
			return (child.transform.GetSiblingIndex () == child.transform.parent.childCount - 1);
		}

		/// <summary>入れ替え</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lhs"></param>
        /// <param name="rhs"></param>
		public static void Swap<T> (ref T lhs, ref T rhs) {
			var temp = lhs;
			lhs = rhs;
			rhs = temp;
		}

		/// <summary>データを16進ダンプする</summary>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
		public static string HexDump (this byte [] data, int width = 16, string separator = " ") {
			var str = new List<string> { };
			for (var i = 0; i < data.Length; i++) {
				str.Add ($"{((i <= 0) ? "" : ((width > 0 && i % width == 0) ? "\n" : separator))}{data [i].ToString ("X2")}");
			}
			return str.Join ("");
		}

		/// <summary>配列を文字列化して連結</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
		public static string Join<T> (this T [] array, string separator) {
			return string.Join (separator, Array.ConvertAll (array, v => v.ToString ()));
		}

		/// <summary>リストを文字列化して連結</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="separator"></param>
        /// <returns></returns>
		public static string Join<T> (this List<T> list, string separator) {
			return (list == null) ? string.Empty : string.Join (separator, list.ConvertAll (v => v.ToString ()));
		}

		/// <summary>Collection<T>がnullまたは空であれば真</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
		public static bool IsNullOrEmpty<T> (this ICollection<T> collection) {
			return (collection == null || collection.Count == 0);
		}

		/// <summary>T []がnullまたは空であれば真</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
		public static bool IsNullOrEmpty<T> (this T [] array) {
			return (array == null || array.Length == 0);
		}

	}

	/// <summary>真偽値ペア</summary>
	public struct Vector2Bool {
        /// <summary></summary>
		public bool x;
        /// <summary></summary>
		public bool y;
        /// <summary></summary>
		public bool And => x & y;
        /// <summary></summary>
		public bool Or => x | y;
        /// <summary></summary>
		public bool Xor => x ^ y;
        /// <summary></summary>
        public static bool operator true (Vector2Bool a) { return a.x & a.y; }
        /// <summary></summary>
		public static bool operator false (Vector2Bool a) { return !a.x & !a.y; }
        /// <summary></summary>
		public static Vector2Bool operator & (Vector2Bool a, Vector2Bool b) { return new Vector2Bool (a.x & b.x, a.y & b.y); }
        /// <summary></summary>
		public static Vector2Bool operator | (Vector2Bool a, Vector2Bool b) { return new Vector2Bool (a.x | b.x, a.y | b.y); }
        /// <summary></summary>
		public static Vector2Bool operator ^ (Vector2Bool a, Vector2Bool b) { return new Vector2Bool (a.x ^ b.x, a.y ^ b.y); }
        /// <summary></summary>
        /// <summary></summary>
		public static Vector2Bool operator ! (Vector2Bool a) { return new Vector2Bool (!a.x, !a.y); }
        /// <summary></summary>
		public static bool operator == (Vector2Bool a, Vector2Bool b) { return a.x == b.x && a.y == b.y; }
        /// <summary></summary>
		public static bool operator != (Vector2Bool a, Vector2Bool b) { return a.x != b.x || a.y != b.y; }
        /// <summary></summary>
        public static readonly Vector2Bool True = new Vector2Bool (true, true);
        /// <summary></summary>
		public static readonly Vector2Bool False = new Vector2Bool (false, false);
        /// <summary></summary>
		public static readonly Vector2Bool XnotY = new Vector2Bool (true, false);
        /// <summary></summary>
		public static readonly Vector2Bool YnotX = new Vector2Bool (false, true);
        /// <summary></summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
		public Vector2Bool (bool x, bool y) {
			this.x = x;
			this.y = y;
		}
        /// <summary></summary>
        /// <param name="obj"></param>
        /// <returns></returns>
		public override bool Equals (object obj) {
			return (obj != null && obj is Vector2Bool && x == ((Vector2Bool) obj).x && y == ((Vector2Bool) obj).y);
		}
        /// <summary></summary>
        /// <returns></returns>
		public override int GetHashCode () {
			return (x ? 1 : 0) + (y ? 2 : 0);
		}
        /// <summary></summary>
        /// <returns></returns>
		public override string ToString () {
			return $"({x}, {y})";
		}
	}

	/// <summary>論理値許容型SystemLanguage</summary>
	public struct Language : IEquatable<Language> {
		private bool hasValue;
		private SystemLanguage language;
        /// <summary></summary>
		public static readonly Language Undef = new Language (false);
        /// <summary></summary>
        /// <param name="_hasValue"></param>
		public Language (bool _hasValue) {
			hasValue = _hasValue;
			language = _hasValue ? Application.systemLanguage : SystemLanguage.Unknown;
		}
        /// <summary></summary>
        /// <param name="_language"></param>
		public Language (SystemLanguage _language) {
			hasValue = _language != SystemLanguage.Unknown;
			language = _language;
		}
        /// <summary></summary>
        /// <param name="name"></param>
        /// <param name="ignoreCase"></param>
        /// <param name="language"></param>
        /// <returns></returns>
		public static bool TryParse (string name, bool ignoreCase, out Language language) {
			if (string.IsNullOrEmpty (name) || !Enum.TryParse (name, ignoreCase, out SystemLanguage syslang)) {
				language = Undef;
				return false;
			}
			language = syslang;
			return true;
		}
        /// <summary></summary>
        /// <param name="name"></param>
        /// <param name="language"></param>
        /// <returns></returns>
		public static bool TryParse (string name, out Language language) => TryParse (name, false, out language);
        /// <summary></summary>
        /// <param name="name"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
		public static Language Parse (string name, bool ignoreCase) { TryParse (name, ignoreCase, out var language); return language; }
        /// <summary></summary>
        /// <param name="name"></param>
        /// <returns></returns>
		public static Language Parse (string name) { TryParse (name, false, out var language); return language; }
        /// <summary></summary>
        /// <returns></returns>
		public SystemLanguage GetValueOrDefault () => hasValue ? language : SystemLanguage.Unknown;
        /// <summary></summary>
        /// <param name="l"></param>
        /// <returns></returns>
		public SystemLanguage GetValueOrDefault (Language l) => hasValue ? language : (SystemLanguage) l;
        /// <summary></summary>
        /// <param name="l"></param>
		public static implicit operator bool (Language l) => l.hasValue;
        /// <summary></summary>
        /// <param name="l"></param>
		public static implicit operator SystemLanguage (Language l) => l.hasValue ? l.language : SystemLanguage.Unknown;
        /// <summary></summary>
        /// <param name="b"></param>
		public static implicit operator Language (bool b) => new Language (b);
        /// <summary></summary>
        /// <param name="l"></param>
		public static implicit operator Language (SystemLanguage l) => new Language (l);
        /// <inheritdoc/>
		public override string ToString () => hasValue ? language.ToString () : "Undef";
        /// <summary></summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
		public static bool operator == (Language a, Language b) => (a.hasValue == b.hasValue) && (!a.hasValue || a.language == b.language);
        /// <summary></summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
		public static bool operator != (Language a, Language b) => !(a == b);
        /// <summary></summary>
        /// <param name="other"></param>
        /// <returns></returns>
		public bool Equals (Language other) => (hasValue == other.hasValue) && (language == other.language);
        /// <inheritdoc/>
		public override bool Equals (object obj) => (obj == null || GetType () != obj.GetType ()) ? false : Equals ((Language) obj);
        /// <inheritdoc/>
		public override int GetHashCode () => hasValue ? language.GetHashCode () : int.MinValue;
	}

}
