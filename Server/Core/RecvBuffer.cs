using System.Buffers;

namespace Server.Core;

public class RecvBuffer
{
    private byte[] _buffer;
    private int _readPos;
    private int _writePos;
    private int _capacity;
    private int _bufferSize;

    public RecvBuffer(int bufferSize)
    {
        _bufferSize = bufferSize;
        
        // 시스템 풀에서 메모리를 빌려옴
        _buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        _capacity = _buffer.Length;
    }
    
    public int DataSize => _writePos - _readPos;
    
    public int FreeSize => _capacity - _writePos;
    
    // 읽을 범위
    public ArraySegment<byte> ReadSegment
    {
        get { return new ArraySegment<byte>(_buffer, _readPos, DataSize); }
    }

    // 쓸 범위
    public ArraySegment<byte> WriteSegment
    {
        get { return new ArraySegment<byte>(_buffer, _writePos, FreeSize); }
    }

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
            Array.Copy(_buffer, _readPos, _buffer, 0, dataSize);
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

    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }
}