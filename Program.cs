using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Xml.XPath;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Security.Cryptography;

namespace Alisa_Deserialization
{
	class Program
	{
		static Dictionary<string, IBinaryType> binaryTypes;

		static Program ()
		{
			binaryTypes = new Dictionary<string, IBinaryType> ();

			binaryTypes.Add ("int", new IntBinaryType());
			binaryTypes.Add ("string", new StringBinaryType());
		}

		interface IStructValue
		{
			string ToString (ref int space);
		}

		class CustomStructValue : IStructValue
		{
			public CustomStructValue (string value, string type)
			{
				this.value = value;
				this.type = type;
			}

			public string value { get; private set; }
			public string type { get; private set; }

			public override string ToString ()
			{
				return value;
			}

			string IStructValue.ToString (ref int space)
			{
				return $"{new string (' ', space)}{type} {value}\n";
			}
		}

		class StructDescription
		{
			public StructDescription (string name, string[] types)
			{
				this.name = name;
				this.types = types;
			}

			public string name { get; private set; }
			public string[] types { get; private set; }

			public override string ToString ()
			{
				string result = $"struct {name}\n";
				for (int i = 0; i < types.Length; i++)
				{
					result += $" {types[i]}\n";
				}
				return result;
			}
		}
		class Struct : StructDescription, IStructValue
		{
			public Struct (string name, string[] types, IStructValue[] values) : base (name, types)
			{
				this.values = values;
			}

			public IStructValue[] values { get; private set; }

			public override string ToString ()
			{
				int space = 0;
				return ToString (ref space);
			}

			public string ToString (ref int space)
			{
				string result = string.Empty;
				string spacing = new string (' ', space);

				result += $"{spacing}{name}\n";

				space++;

				for (int i = 0; i < values.Length; i++)
				{
					result += spacing + values[i].ToString (ref space);
				}

				space--;

				return result;
			}
		}

		interface IBinaryType
		{
			string ReadType (string binary, ref int position);
			string WriteType (string value);
		}

		class IntBinaryType : IBinaryType
		{
			public string ReadType (string binary, ref int position)
			{
				int start = position;
				position += 8;
				return int.Parse (binary.Substring (start, 8), NumberStyles.HexNumber).ToString ();
			}

			public string WriteType (string value)
			{
				int i = int.Parse (value);
				return i.ToString("X8");
			}
		}

		class StringBinaryType : IBinaryType
		{
			public string ReadType (string binary, ref int position)
			{
				int start = position;

				int length = int.Parse(binaryTypes["int"].ReadType (binary, ref position));

				byte[] bytes = new byte[length];

				for (int i = 0; i < length; i++)
				{
					int s = start + 8 + i * 2;
					string desc = binary.Substring (s, 2);
					bytes[i] = byte.Parse (desc, NumberStyles.HexNumber);

					position += 2;
				}
				return Encoding.ASCII.GetString (bytes);
			}

			public string WriteType (string value)
			{
				string result = binaryTypes["int"].WriteType (value.Length.ToString());
				byte[] bytes = Encoding.ASCII.GetBytes (value);

				for (int i = 0; i < value.Length; i++) result += bytes[i].ToString("X");

				return result;
			}
		}

		class BinaryReader
		{
			public Struct ReadBinary (StructDescription[] structs, string binary)
			{
				int position = 0;

				List<Struct> ss = new List<Struct> ();

				while (position < binary.Length)
				{
					int stIndex = int.Parse (binaryTypes["int"].ReadType (binary, ref position));
					var read = ReadBinary (structs[stIndex - 1], structs, binary, ref position);
					ss.Add (read);
				}

				return new Struct ("global", ss.Select (s => s.name).ToArray(), ss.ToArray ());
			}

			public Struct ReadBinary (StructDescription desc, StructDescription[] structs, string binary, ref int position)
			{
				IStructValue[] values = new IStructValue[desc.types.Length];

				for (int i = 0; i < values.Length; i++)
				{
					string type = desc.types[i];
					if (binaryTypes.ContainsKey (type))
					{
						values[i] = new CustomStructValue (binaryTypes[type].ReadType (binary, ref position), type);
					}
					else
					{
						var st = structs.First (s => s.name.Equals (type));
						values[i] = ReadBinary (st, structs, binary, ref position);
					}
				}

				return new Struct (desc.name, desc.types, values);
			}
		}
		class BinaryWriter
		{
			public string WriteBinary (Struct str)
			{
				List<string> structTypes = new List<string> ();
				return WriteBinary (str, structTypes);
			}

			private string WriteBinary (Struct str, List<string> structTypes)
			{
				string result = string.Empty;
				if (!structTypes.Contains (str.name)) structTypes.Add (str.name);

				for (int i = 0; i < str.types.Length; i++)
				{
					string type = str.types[i];

					if (binaryTypes.ContainsKey (type))
					{
						result += binaryTypes[type].WriteType (str.values[i].ToString ());
					}
					else
					{
						result += binaryTypes["int"].WriteType (structTypes.IndexOf(type).ToString());
						result += WriteBinary ((Struct)str.values[i], structTypes);
					}
				}

				return result;
			}
		}
		static void Main (string[] args)
		{
			var reader = new BinaryReader ();
			var writer = new BinaryWriter ();

			string[] sData = Console.ReadLine ().Split();

			int structuresCount = int.Parse(sData[0]);
			int structuresLines = int.Parse(sData[1]);

			string name = null;
			List<string> types = new List<string> ();
			StructDescription[] structs = new StructDescription[structuresCount];
			int currentStruct = 0;
			string binary_global = string.Empty;

			for (int i = 0; i < structuresLines; i++)
			{
				string[] words = Console.ReadLine ().Split ();
				if (words[0].Equals ("struct"))
				{
					if (name != null)
					{
						structs[currentStruct] = new StructDescription (name, types.ToArray ());
						currentStruct++;
						types.Clear ();
					}

					string addIndex = binaryTypes["int"].WriteType ((currentStruct + 1).ToString ());
					binary_global += addIndex;

					name = words[1];
				}
				else
				{
					types.Add (words[0]);
				}
			}

			structs[currentStruct] = new StructDescription (name, types.ToArray ());

			string binary = Console.ReadLine ();

			var read = reader.ReadBinary (structs, binary);

			string output = string.Empty;

			foreach (var s in read.values) output += s.ToString ();

			Console.WriteLine (output);
		}
	}
}
