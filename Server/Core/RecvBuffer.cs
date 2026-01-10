using System.Buffers;

namespace Server.Core;

public class RecvBuffer
{
    private ArraySegment<byte> _buffer;
    private int _readPos;
    private int _writePos;

    private byte[] _rentedBuffer;
    private bool _disposedValue;

    public RecvBuffer(int bufferSize)
    {
        _rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        
        _buffer = new ArraySegment<byte>(new byte[bufferSize], 0 , bufferSize);
    }
    
    public int DataSize => _writePos - _readPos;
    public int FreeSize => _buffer.Count - _writePos;
    
    public ArraySegment<byte> ReadSegment => new(_buffer.Array, _buffer.Offset + _readPos, DataSize);
    public ArraySegment<byte> WriteSegment => new(_buffer.Array, _buffer.Offset + _writePos, FreeSize);

    public void Clean()
    {
        int dataSize = DataSize;
        if (dataSize == 0)
        {
            _readPos = _writePos = 0;
        }
        else
        {
            // 남은 데이터가 있으면 시작 지점으로 복사
            Array.Copy(_buffer.Array, _buffer.Offset + _readPos, _buffer.Array, _buffer.Offset, dataSize);
            _readPos = 0;
            _writePos = dataSize;
        }
    }

    public bool OnRead(int numOfBytes)
    {
        if (numOfBytes > DataSize) return false;
        _readPos += numOfBytes;
        return true;
    }

    public bool OnWrite(int numOfBytes)
    {
        if (numOfBytes > FreeSize) return false;
        _writePos += numOfBytes;
        return true;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                if (_rentedBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_rentedBuffer);
                }
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}