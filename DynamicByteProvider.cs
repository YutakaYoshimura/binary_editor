using System;
using System.Collections.Generic;

namespace Be.Windows.Forms
{
    // ===== 追加：課題① Undo/Redo 用コマンドインターフェースと実装クラス =====

    /// <summary>
    /// Undo/Redo 操作を抽象化するコマンドインターフェース。
    /// </summary>
    internal interface IHexEditCommand
    {
        void Execute(DynamicByteProvider provider);
        void Undo(DynamicByteProvider provider);
    }

    /// <summary>
    /// 1バイトの上書きコマンド。
    /// </summary>
    internal class WriteByteCommand : IHexEditCommand
    {
        private readonly long _index;
        private readonly byte _newValue;
        private byte _oldValue;

        public WriteByteCommand(long index, byte newValue)
        {
            _index = index;
            _newValue = newValue;
        }

        public void Execute(DynamicByteProvider p)
        {
            _oldValue = p.ReadByte(_index);
            p.WriteByteInternal(_index, _newValue);
        }

        public void Undo(DynamicByteProvider p)
        {
            p.WriteByteInternal(_index, _oldValue);
        }
    }

    /// <summary>
    /// バイト列の挿入コマンド。
    /// </summary>
    internal class InsertBytesCommand : IHexEditCommand
    {
        private readonly long _index;
        private readonly byte[] _bytes;

        public InsertBytesCommand(long index, byte[] bytes)
        {
            _index = index;
            _bytes = (byte[])bytes.Clone();
        }

        public void Execute(DynamicByteProvider p)
        {
            p.InsertBytesInternal(_index, _bytes);
        }

        public void Undo(DynamicByteProvider p)
        {
            p.DeleteBytesInternal(_index, _bytes.LongLength);
        }
    }

    /// <summary>
    /// バイト列の削除コマンド。
    /// </summary>
    internal class DeleteBytesCommand : IHexEditCommand
    {
        private readonly long _index;
        private readonly long _length;
        private byte[] _deletedBytes;

        public DeleteBytesCommand(long index, long length)
        {
            _index = index;
            _length = length;
        }

        public void Execute(DynamicByteProvider p)
        {
            _deletedBytes = new byte[_length];
            for (long i = 0; i < _length; i++)
                _deletedBytes[i] = p.ReadByte(_index + i);
            p.DeleteBytesInternal(_index, _length);
        }

        public void Undo(DynamicByteProvider p)
        {
            p.InsertBytesInternal(_index, _deletedBytes);
        }
    }

    // ===== 追加ここまで =====

    /// <summary>
    /// Byte provider for a small amount of data.
    /// </summary>
    public class DynamicByteProvider : IByteProvider
    {
        bool _hasChanges;
        List<byte> _bytes;

        // ===== 追加：課題① Undo/Redo スタック =====
        private Stack<IHexEditCommand> _undoStack = new Stack<IHexEditCommand>();
        private Stack<IHexEditCommand> _redoStack = new Stack<IHexEditCommand>();
        // ===== 追加ここまで =====

        public DynamicByteProvider(byte[] data) : this(new List<Byte>(data)) { }

        public DynamicByteProvider(List<Byte> bytes)
        {
            _bytes = bytes;
        }

        void OnChanged(EventArgs e)
        {
            _hasChanges = true;
            if (Changed != null)
                Changed(this, e);
        }

        void OnLengthChanged(EventArgs e)
        {
            if (LengthChanged != null)
                LengthChanged(this, e);
        }

        public List<Byte> Bytes
        {
            get { return _bytes; }
        }

        #region IByteProvider Members

        public bool HasChanges()
        {
            return _hasChanges;
        }

        public void ApplyChanges()
        {
            _hasChanges = false;
        }

        public event EventHandler Changed;
        public event EventHandler LengthChanged;

        public byte ReadByte(long index)
        { return _bytes[(int)index]; }

        /// <summary>
        /// Write a byte into the byte collection.
        /// [修正①] コマンド経由で実行し Undo スタックに積む。
        /// </summary>
        public void WriteByte(long index, byte value)
        {
            var cmd = new WriteByteCommand(index, value);
            cmd.Execute(this);
            _undoStack.Push(cmd);
            _redoStack.Clear();
            OnChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Deletes bytes from the byte collection.
        /// [修正①] コマンド経由で実行し Undo スタックに積む。
        /// </summary>
        public void DeleteBytes(long index, long length)
        {
            var cmd = new DeleteBytesCommand(index, length);
            cmd.Execute(this);
            _undoStack.Push(cmd);
            _redoStack.Clear();
            OnLengthChanged(EventArgs.Empty);
            OnChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Inserts byte into the byte collection.
        /// [修正①] コマンド経由で実行し Undo スタックに積む。
        /// </summary>
        public void InsertBytes(long index, byte[] bs)
        {
            var cmd = new InsertBytesCommand(index, bs);
            cmd.Execute(this);
            _undoStack.Push(cmd);
            _redoStack.Clear();
            OnLengthChanged(EventArgs.Empty);
            OnChanged(EventArgs.Empty);
        }

        public long Length
        {
            get { return _bytes.Count; }
        }

        public bool SupportsWriteByte() { return true; }
        public bool SupportsInsertBytes() { return true; }
        public bool SupportsDeleteBytes() { return true; }

        #endregion

        // ===== 追加：課題① 内部操作メソッド（コマンドが直接呼ぶ。スタックに積まない）=====

        internal void WriteByteInternal(long index, byte value)
        {
            _bytes[(int)index] = value;
        }

        internal void InsertBytesInternal(long index, byte[] bs)
        {
            _bytes.InsertRange((int)index, bs);
        }

        internal void DeleteBytesInternal(long index, long length)
        {
            int internal_index = (int)Math.Max(0, index);
            int internal_length = (int)Math.Min((int)Length, length);
            _bytes.RemoveRange(internal_index, internal_length);
        }

        // ===== 追加：課題① Undo/Redo 公開メソッド・プロパティ =====

        /// <summary>Undo が可能かどうかを返す。</summary>
        public bool CanUndo
        {
            get { return _undoStack.Count > 0; }
        }

        /// <summary>Redo が可能かどうかを返す。</summary>
        public bool CanRedo
        {
            get { return _redoStack.Count > 0; }
        }

        /// <summary>直前の操作を取り消す。</summary>
        public void Undo()
        {
            if (!CanUndo) return;
            var cmd = _undoStack.Pop();
            cmd.Undo(this);
            _redoStack.Push(cmd);
            OnLengthChanged(EventArgs.Empty);
            OnChanged(EventArgs.Empty);
        }

        /// <summary>取り消した操作をやり直す。</summary>
        public void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redoStack.Pop();
            cmd.Execute(this);
            _undoStack.Push(cmd);
            OnLengthChanged(EventArgs.Empty);
            OnChanged(EventArgs.Empty);
        }

        /// <summary>Undo/Redo 履歴をすべてクリアする。</summary>
        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        // ===== 追加ここまで =====
    }
}
