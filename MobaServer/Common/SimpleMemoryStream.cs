using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyLib
{
    public class SimpleMemoryStream : System.IO.Stream
    {
    	public const int BUFFER_MAX = 1460 * 4;
    	private byte[] datas_ = new byte[BUFFER_MAX]; 

    	public int wpos = 0;

        public byte[] GetBuffer()
        {
            return datas_;
        }

        public void Reset()
        {
            wpos = 0;
        }

        public override void Flush()
        {
           throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            //LogHelper.Log("Stream", "WriteData: "+offset+" count "+count);
            Array.Copy(buffer, offset, datas_, wpos, count);
            wpos += count;
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { return wpos; }
        }

        public override long Position
        {
            get { return wpos; }
            set
            {
                
            }
        }
    }
}
