using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using CustomMsgPack;

namespace MsgPackExplorer
{
    public partial class LsMsgPackExplorer : UserControl
    {
        public LsMsgPackExplorer()
        {
            InitializeComponent();
        }

        private CustomMsgPackItem item;

        [Category("MsgPack")]
        [DisplayName("Item")]
        [Description("The root element of a MsgPack message.")]
        public CustomMsgPackItem Item
        {
            get { return item; }
            set
            {
                item = value;
                RefreshTree();
            }
        }

        private bool _continueOnError;
        [Category("MsgPack")]
        [DisplayName("Continue On Error")]
        [Description("Set this to true in order to keep processing the stream after a breaking error occurred.")]
        public bool ContinueOnError
        {
            get { return _continueOnError; }
            set
            {
                _continueOnError = value;
            }
        }

        private long _displayLimit = 1000;
        [Category("MsgPack")]
        [DisplayName("Limit items")]
        [Description("Limit the number of items that are displayed when many items are processed.")]
        public long DisplayLimit
        {
            get { return _displayLimit; }
            set { _displayLimit = value; }
        }

        public enum EndianAction
        {
            SwapIfCurrentSystemIsLittleEndian,
            NeverSwap,
            AlwaysSwap,
        }


        private EndianAction _endianHandling = EndianAction.SwapIfCurrentSystemIsLittleEndian;
        [Category("MsgPack")]
        [DisplayName("Endian handling")]
        [Description("Override Endianess conversion (default will reorder bytes on little-endian systems).")]
        public EndianAction EndianHandling
        {
            get { return _endianHandling; }
            set { _endianHandling = value; }
        }


        private byte[] data;
        [Category("MsgPack")]
        [DisplayName("Data")]
        [Description("The raw original bytes of a MsgPack message.")]
        public byte[] Data
        {
            get { return data; }
            set
            {
                data = value;
                if (ReferenceEquals(value, null)) Item = null;
                else
                {
                    var packer = new CustomBoxingPacker();
                    Item = packer.Unpack(data);
                }
            }
        }

        public void Clear()
        {
            Data = null;
        }

        public Image GetIcon()
        {
            using (var stream = typeof(LsMsgPackExplorer).Assembly.GetManifestResourceStream("Explore"))
            {
                if (stream != null)
                {
                    return Image.FromStream(stream);
                }
            }
            return null;
        }

        List<EditorMetaData> lineairList = new List<EditorMetaData>();

        private class EditorMetaData
        {
            public int CharOffset = 0;
            public int Length = 0;
            public TreeNode Node;
            public CustomMsgPackItem Item;
        }

        public void RefreshTree()
        {
            SuspendLayout();
            treeView1.SuspendLayout();
            treeView1.BeginUpdate();
            richTextBox1.SuspendLayout();
            Cursor = Cursors.WaitCursor;
            try
            {
                treeView1.Nodes.Clear();
                richTextBox1.Clear();
                lineairList.Clear();
                listView1.Items.Clear();
                if (ReferenceEquals(item, null)) return;

                TreeNode root = GetTreeNodeFor(item);
                Traverse(root, item);

                treeView1.Nodes.Add(root);
                treeView1.ExpandAll();
                if (ReferenceEquals(data, null) || data.Length == 0) return;

                string[] hex = BitConverter.ToString(data).Split('-');
                StringBuilder sb = new StringBuilder("{\\rtf1 {\\colortbl ;\\red255\\green0\\blue0;\\red0\\green77\\blue187;\\red127\\green127\\blue127;}\r\n");
                int byteOffset = 0;

                EditorMetaData meta = null;

                if (!ReferenceEquals(meta, null) && !ReferenceEquals(meta.Item, null))
                {
                    while (byteOffset < data.Length)
                    {
                        sb.Append(hex[byteOffset]).Append(' ');
                        byteOffset++;
                    }
                }

                if (hex.Length - 1 > byteOffset) sb.Append("\\cf3 "); // gray
                while (hex.Length - 1 > byteOffset)
                {
                    sb.Append(hex[byteOffset]).Append(' ');
                    byteOffset++;
                }

                sb.Append("\r\n}\r\n");
                richTextBox1.Rtf = sb.ToString();
            }
            finally
            {
                ResumeLayout();
                treeView1.EndUpdate();
                treeView1.ResumeLayout();
                richTextBox1.ResumeLayout();
                Cursor = Cursors.Default;
            }
        }

        private TreeNode GetTreeNodeFor(CustomMsgPackItem item)
        {
            int imgIdx = GetIconFor(item);
            string text = ReferenceEquals(item, null) ? "NULL" : item.ToString();
            int pos = text.IndexOfAny(new char[] { '\r', '\n' });
            if (pos > 0) text = text.Substring(0, pos - 1);
            TreeNode node = new TreeNode(text, imgIdx, imgIdx);
            node.Tag = item;
            return node;
        }

        private void Traverse(TreeNode node, CustomMsgPackItem item)
        {
            if (ReferenceEquals(item, null)) return;

            if (item is CustomMpArray arr)
            {
                foreach (var childItem in arr.Items)
                {
                    TreeNode childNode = GetTreeNodeFor(childItem);
                    node.Nodes.Add(childNode);
                    Traverse(childNode, childItem);
                }
            }
            else if (item is CustomMpMap map)
            {
                foreach (var kvp in map.Items)
                {
                    TreeNode keyNode = GetTreeNodeFor(kvp.Key);
                    keyNode.StateImageIndex = 8; // Key
                    node.Nodes.Add(keyNode);
                    Traverse(keyNode, kvp.Key);

                    TreeNode valueNode = GetTreeNodeFor(kvp.Value);
                    valueNode.StateImageIndex = 9; // Value
                    keyNode.Nodes.Add(valueNode);
                    Traverse(valueNode, kvp.Value);
                }
            }
        }

        private int GetIconFor(CustomMsgPackItem item)
        {
            if (item is CustomMpNull) return 0;
            if (item is CustomMpBoolean) return 1;
            if (item is CustomMpInteger) return 2;
            if (item is CustomMpFloat) return 3;
            if (item is CustomMpBinary) return 4;
            if (item is CustomMpString) return 5;
            if (item is CustomMpArray) return 6;
            if (item is CustomMpMap) return 7;
            return -1;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        private const int WM_SETREDRAW = 0x0b;

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (ReferenceEquals(e.Node, null))
            {
                propertyGrid1.SelectedObject = null;
                return;
            }

            propertyGrid1.SelectedObject = e.Node.Tag;
            propertyGrid1.ExpandAllGridItems();
        }

        private void richTextBox1_SelectionChanged(object sender, EventArgs e)
        {
            // For future implementation if needed
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // For future implementation if needed
        }

        private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            e.DrawDefault = true;
        }
    }
}

namespace CustomMsgPack
{
    public abstract class CustomMsgPackItem
    {
        public abstract object Value { get; set; }
        public abstract string TypeName { get; }
        public override string ToString()
        {
            return $"{TypeName}: {Value}";
        }
    }

    public class CustomMpNull : CustomMsgPackItem
    {
        public override object Value { get; set; } = null;
        public override string TypeName => "Null";
        public override string ToString() => "[NULL]";
    }

    public class CustomMpBoolean : CustomMsgPackItem
    {
        public override object Value { get; set; }
        public override string TypeName => "Boolean";
    }

    public class CustomMpInteger : CustomMsgPackItem
    {
        public override object Value { get; set; }
        public override string TypeName => "Integer";
    }

    public class CustomMpFloat : CustomMsgPackItem
    {
        public override object Value { get; set; }
        public override string TypeName => "Float";
    }

    public class CustomMpString : CustomMsgPackItem
    {
        public override object Value { get; set; }
        public override string TypeName => "String";
    }

    public class CustomMpBinary : CustomMsgPackItem
    {
        public override object Value { get; set; }
        public override string TypeName => "Binary";
        public override string ToString()
        {
            if (Value is byte[] bytes)
            {
                return $"Binary: {bytes.Length} bytes";
            }
            return "Binary: (empty)";
        }
    }

    public class CustomMpArray : CustomMsgPackItem
    {
        public override object Value { get; set; }
        public override string TypeName => "Array";
        public CustomMsgPackItem[] Items { get; set; }
        public override string ToString()
        {
            return $"Array ({(Items?.Length ?? 0)} items)";
        }
    }

    public class CustomMpMap : CustomMsgPackItem
    {
        public override object Value { get; set; }
        public override string TypeName => "Map";
        public KeyValuePair<CustomMsgPackItem, CustomMsgPackItem>[] Items { get; set; }
        public override string ToString()
        {
            return $"Map ({(Items?.Length ?? 0)} items)";
        }
    }

    public class CustomBoxingPacker
    {
        public CustomMsgPackItem Unpack(byte[] buf)
        {
            using (var ms = new MemoryStream(buf))
            {
                var reader = new CustomMsgPackReader(ms);
                return Unpack(reader);
            }
        }

        private CustomMsgPackItem Unpack(CustomMsgPackReader reader)
        {
            if (!reader.Read())
            {
                throw new FormatException();
            }

            switch (reader.Type)
            {
                case TypePrefixes.PositiveFixNum:
                case TypePrefixes.Int8:
                case TypePrefixes.Int16:
                case TypePrefixes.Int32:
                case TypePrefixes.NegativeFixNum:
                    return new CustomMpInteger { Value = reader.ValueSigned };
                case TypePrefixes.Int64:
                    return new CustomMpInteger { Value = reader.ValueSigned64 };
                case TypePrefixes.UInt8:
                case TypePrefixes.UInt16:
                case TypePrefixes.UInt32:
                    return new CustomMpInteger { Value = reader.ValueUnsigned };
                case TypePrefixes.UInt64:
                    return new CustomMpInteger { Value = reader.ValueUnsigned64 };
                case TypePrefixes.True:
                    return new CustomMpBoolean { Value = true };
                case TypePrefixes.False:
                    return new CustomMpBoolean { Value = false };
                case TypePrefixes.Float:
                    return new CustomMpFloat { Value = reader.ValueFloat };
                case TypePrefixes.Double:
                    return new CustomMpFloat { Value = reader.ValueDouble };
                case TypePrefixes.Nil:
                    return new CustomMpNull();
                case TypePrefixes.FixRaw:
                case TypePrefixes.Raw8:
                case TypePrefixes.Raw16:
                case TypePrefixes.Raw32:
                    {
                        byte[] array2 = new byte[reader.Length];
                        reader.ReadValueRaw(array2, 0, array2.Length);
                        try
                        {
                            // Try to decode as string
                            return new CustomMpString { Value = Encoding.UTF8.GetString(array2) };
                        }
                        catch
                        {
                            // If it fails, treat as binary
                            return new CustomMpBinary { Value = array2 };
                        }
                    }
                case TypePrefixes.FixArray:
                case TypePrefixes.Array16:
                case TypePrefixes.Array32:
                    {
                        var items = new CustomMsgPackItem[reader.Length];
                        for (int i = 0; i < items.Length; i++)
                        {
                            items[i] = Unpack(reader);
                        }
                        return new CustomMpArray { Items = items, Value = items };
                    }
                case TypePrefixes.FixMap:
                case TypePrefixes.Map16:
                case TypePrefixes.Map32:
                    {
                        var items = new KeyValuePair<CustomMsgPackItem, CustomMsgPackItem>[reader.Length];
                        for (int i = 0; i < items.Length; i++)
                        {
                            var key = Unpack(reader);
                            var value = Unpack(reader);
                            items[i] = new KeyValuePair<CustomMsgPackItem, CustomMsgPackItem>(key, value);
                        }
                        return new CustomMpMap { Items = items, Value = items };
                    }
                default:
                    throw new FormatException();
            }
        }
    }

    public class CustomMsgPackReader
    {
        private Stream _strm;
        private byte[] _tmp0 = new byte[8];
        private byte[] _tmp1 = new byte[8];

        public TypePrefixes Type { get; private set; }
        public bool ValueBoolean { get; private set; }
        public uint Length { get; private set; }
        public uint ValueUnsigned { get; private set; }
        public ulong ValueUnsigned64 { get; private set; }
        public int ValueSigned { get; private set; }
        public long ValueSigned64 { get; private set; }
        public float ValueFloat { get; private set; }
        public double ValueDouble { get; private set; }

        public CustomMsgPackReader(Stream strm)
        {
            _strm = strm;
        }

        public bool Read()
        {
            byte[] tmp = _tmp0;
            byte[] tmp2 = _tmp1;
            int num = _strm.ReadByte();
            if (num < 0) return false;

            if (num >= 0 && num <= 127) Type = TypePrefixes.PositiveFixNum;
            else if (num >= 224 && num <= 255) Type = TypePrefixes.NegativeFixNum;
            else if (num >= 160 && num <= 191) Type = TypePrefixes.FixRaw;
            else if (num >= 144 && num <= 159) Type = TypePrefixes.FixArray;
            else if (num >= 128 && num <= 143) Type = TypePrefixes.FixMap;
            else Type = (TypePrefixes)num;

            switch (Type)
            {
                case TypePrefixes.False: ValueBoolean = false; break;
                case TypePrefixes.True: ValueBoolean = true; break;
                case TypePrefixes.Float:
                    _strm.Read(tmp, 0, 4);
                    if (BitConverter.IsLittleEndian)
                    {
                        tmp2[0] = tmp[3]; tmp2[1] = tmp[2]; tmp2[2] = tmp[1]; tmp2[3] = tmp[0];
                        ValueFloat = BitConverter.ToSingle(tmp2, 0);
                    }
                    else ValueFloat = BitConverter.ToSingle(tmp, 0);
                    break;
                case TypePrefixes.Double:
                    _strm.Read(tmp, 0, 8);
                    if (BitConverter.IsLittleEndian)
                    {
                        tmp2[0] = tmp[7]; tmp2[1] = tmp[6]; tmp2[2] = tmp[5]; tmp2[3] = tmp[4];
                        tmp2[4] = tmp[3]; tmp2[5] = tmp[2]; tmp2[6] = tmp[1]; tmp2[7] = tmp[0];
                        ValueDouble = BitConverter.ToDouble(tmp2, 0);
                    }
                    else ValueDouble = BitConverter.ToDouble(tmp, 0);
                    break;
                case TypePrefixes.NegativeFixNum: ValueSigned = (sbyte)num; break;
                case TypePrefixes.PositiveFixNum: ValueUnsigned = (byte)num; ValueSigned = (sbyte)num; break;
                case TypePrefixes.UInt8: ValueUnsigned = (byte)_strm.ReadByte(); break;
                case TypePrefixes.UInt16: _strm.Read(tmp, 0, 2); ValueUnsigned = (uint)((tmp[0] << 8) | tmp[1]); break;
                case TypePrefixes.UInt32: _strm.Read(tmp, 0, 4); ValueUnsigned = (uint)((tmp[0] << 24) | (tmp[1] << 16) | (tmp[2] << 8) | tmp[3]); break;
                case TypePrefixes.UInt64: _strm.Read(tmp, 0, 8); ValueUnsigned64 = (((ulong)tmp[0] << 56) | ((ulong)tmp[1] << 48) | ((ulong)tmp[2] << 40) | ((ulong)tmp[3] << 32) | ((ulong)tmp[4] << 24) | ((ulong)tmp[5] << 16) | ((ulong)tmp[6] << 8) | tmp[7]); break;
                case TypePrefixes.Int8: ValueSigned = (sbyte)_strm.ReadByte(); break;
                case TypePrefixes.Int16: _strm.Read(tmp, 0, 2); ValueSigned = (short)((tmp[0] << 8) | tmp[1]); break;
                case TypePrefixes.Int32: _strm.Read(tmp, 0, 4); ValueSigned = (int)((tmp[0] << 24) | (tmp[1] << 16) | (tmp[2] << 8) | tmp[3]); break;
                case TypePrefixes.Int64: _strm.Read(tmp, 0, 8); ValueSigned64 = (long)(((ulong)tmp[0] << 56) | ((ulong)tmp[1] << 48) | ((ulong)tmp[2] << 40) | ((ulong)tmp[3] << 32) | ((ulong)tmp[4] << 24) | ((ulong)tmp[5] << 16) | ((ulong)tmp[6] << 8) | tmp[7]); break;
                case TypePrefixes.FixRaw: Length = (uint)(num & 0x1F); break;
                case TypePrefixes.FixArray: Length = (uint)(num & 0x0F); break;
                case TypePrefixes.FixMap: Length = (uint)(num & 0x0F); break;
                case TypePrefixes.Raw8: Length = (byte)_strm.ReadByte(); break;
                case TypePrefixes.Raw16: _strm.Read(tmp, 0, 2); Length = (uint)((tmp[0] << 8) | tmp[1]); break;
                case TypePrefixes.Raw32: _strm.Read(tmp, 0, 4); Length = (uint)((tmp[0] << 24) | (tmp[1] << 16) | (tmp[2] << 8) | tmp[3]); break;
                case TypePrefixes.Array16: _strm.Read(tmp, 0, 2); Length = (uint)((tmp[0] << 8) | tmp[1]); break;
                case TypePrefixes.Array32: _strm.Read(tmp, 0, 4); Length = (uint)((tmp[0] << 24) | (tmp[1] << 16) | (tmp[2] << 8) | tmp[3]); break;
                case TypePrefixes.Map16: _strm.Read(tmp, 0, 2); Length = (uint)((tmp[0] << 8) | tmp[1]); break;
                case TypePrefixes.Map32: _strm.Read(tmp, 0, 4); Length = (uint)((tmp[0] << 24) | (tmp[1] << 16) | (tmp[2] << 8) | tmp[3]); break;
                case TypePrefixes.Nil: break;
                default: throw new FormatException();
            }
            return true;
        }

        public int ReadValueRaw(byte[] buf, int offset, int count)
        {
            return _strm.Read(buf, offset, count);
        }
    }

    public enum TypePrefixes : byte
    {
        PositiveFixNum = 0x00,
        FixMap = 0x80,
        FixArray = 0x90,
        FixRaw = 0xa0,
        Nil = 0xc0,
        False = 0xc2,
        True = 0xc3,
        Float = 0xca,
        Double = 0xcb,
        UInt8 = 0xcc,
        UInt16 = 0xcd,
        UInt32 = 0xce,
        UInt64 = 0xcf,
        Int8 = 0xd0,
        Int16 = 0xd1,
        Int32 = 0xd2,
        Int64 = 0xd3,
        Raw8 = 0xd9,
        Raw16 = 0xda,
        Raw32 = 0xdb,
        Array16 = 0xdc,
        Array32 = 0xdd,
        Map16 = 0xde,
        Map32 = 0xdf,
        NegativeFixNum = 0xe0
    }
}