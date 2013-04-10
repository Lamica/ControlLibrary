using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace ControlLibrary.GifSynthesis
{
    /// <summary>
    /// ȱ��GIFͼƬÿ֡��Left,Top �͹���ͼ�����Ϣ��
    /// ����ֻ��һ�����ϳ�GIF�ĺϳ���
    /// </summary>
    public class LZWEncoder
    {

        private static readonly int EOF = -1;

        private int imgW, imgH;
        private byte[] pixAry;
        private int initCodeSize;
        private int remaining;
        private int curPixel;

        // GIFCOMPR.C       - GIF Image compression routines
        //
        // Lempel-Ziv compression based on 'compress'.  GIF modifications by
        // David Rowley (mgardi@watdcsu.waterloo.edu)

        // General DEFINEs

        static readonly int BITS = 12;

        static readonly int HSIZE = 5003; // 80% occupancy

        // GIF Image compression - modified 'compress'
        //
        // Based on: compress.c - File compression ala IEEE Computer, June 1984.
        //
        // By Authors:  Spencer W. Thomas      (decvax!harpo!utah-cs!utah-gr!thomas)
        //              Jim McKie              (decvax!mcvax!jim)
        //              Steve Davies           (decvax!vax135!petsd!peora!srd)
        //              Ken Turkowski          (decvax!decwrl!turtlevax!ken)
        //              James A. Woods         (decvax!ihnp4!ames!jaw)
        //              Joe Orost              (decvax!vax135!petsd!joe)

        int n_bits; // number of bits/code
        int maxbits = BITS; // user settable max # bits/code
        int maxcode; // maximum code, given n_bits
        int maxmaxcode = 1 << BITS; // should NEVER generate this code

        int[] htab = new int[HSIZE]; //����Ƿ�hash��Ͳ��,����������Ժܿ���ҵ�1��key
        int[] codetab = new int[HSIZE];

        int hsize = HSIZE; // for dynamic table sizing

        int free_ent = 0; // first unused entry

        // block compression parameters -- after all codes are used up,
        // and compression rate changes, start over.
        bool clear_flg = false;

        // Algorithm:  use open addressing double hashing (no chaining) on the
        // prefix code / next character combination.  We do a variant of Knuth's
        // algorithm D (vol. 3, sec. 6.4) along with G. Knott's relatively-prime
        // secondary probe.  Here, the modular division first probe is gives way
        // to a faster exclusive-or manipulation.  Also do block compression with
        // an adaptive reset, whereby the code table is cleared when the compression
        // ratio decreases, but after the table fills.  The variable-length output
        // codes are re-sized at this point, and a special CLEAR code is generated
        // for the decompressor.  Late addition:  construct the table according to
        // file size for noticeable speed improvement on small files.  Please direct
        // questions about this implementation to ames!jaw.

        int g_init_bits;

        int ClearCode;
        int EOFCode;

        // output
        //
        // Output the given code.
        // Inputs:
        //      code:   A n_bits-bit integer.  If == -1, then EOF.  This assumes
        //              that n_bits =< wordsize - 1.
        // Outputs:
        //      Outputs code to the file.
        // Assumptions:
        //      Chars are 8 bits long.
        // Algorithm:
        //      Maintain a BITS character long buffer (so that 8 codes will
        // fit in it exactly).  Use the VAX insv instruction to insert each
        // code in turn.  When the buffer fills up empty it and start over.

        int cur_accum = 0;
        int cur_bits = 0;

        int[] masks =
		{
			0x0000,
			0x0001,
			0x0003,
			0x0007,
			0x000F,
			0x001F,
			0x003F,
			0x007F,
			0x00FF,
			0x01FF,
			0x03FF,
			0x07FF,
			0x0FFF,
			0x1FFF,
			0x3FFF,
			0x7FFF,
			0xFFFF };

        // Number of characters so far in this 'packet'
        int a_count;

        // Define the storage for the packet accumulator
        byte[] accum = new byte[256];

        //----------------------------------------------------------------------------
        public LZWEncoder(int width, int height, byte[] pixels, int color_depth)
        {
            imgW = width;
            imgH = height;
            pixAry = pixels;
            initCodeSize = Math.Max(2, color_depth);
        }

        // Add a character to the end of the current packet, and if it is 254
        // characters, flush the packet to disk.
        void Add(byte c, Stream outs)
        {
            accum[a_count++] = c;
            if (a_count >= 254)
                Flush(outs);
        }

        // Clear out the hash table

        // table clear for block compress
        void ClearTable(Stream outs)
        {
            ResetCodeTable(hsize);
            free_ent = ClearCode + 2;
            clear_flg = true;

            Output(ClearCode, outs);
        }

        // reset code table
        // ȫ����ʼ��Ϊ-1
        void ResetCodeTable(int hsize)
        {
            for (int i = 0; i < hsize; ++i)
                htab[i] = -1;
        }

        void Compress(int init_bits, Stream outs)
        {
            int fcode;
            int i /* = 0 */;
            int c;
            int ent;
            int disp;
            int hsize_reg;
            int hshift;

            // Set up the globals:  g_init_bits - initial number of bits
            //ԭʼ���ݵ��ֳ�,��gif�ļ��У�ԭʼ���ݵ��ֳ�����Ϊ1(��ɫͼ),4(16ɫ)����8(256ɫ)
            //��ʼ��ʱ���ȼ���1
            //���ǵ�ԭʼ���ݳ���Ϊ1��ʱ�򣬿�ʼΪ3
            //���ԭʼ����1->3,4->5,8->9

            //?Ϊ��ԭʼ�����ֳ�Ϊ1��ʱ�򣬿�ʼ����Ϊ3�أ�?
            //���+1=2��ֻ�ܱ�ʾ����״̬������clearcode��endcode�������ˡ����Ա�����չ��3
            g_init_bits = init_bits;

            // Set up the necessary values
            //�Ƿ���Ҫ�������־
            //GIFΪ�����ѹ���ʣ����õ��Ǳ䳤���ֳ�(VCL)������˵ԭʼ������8λ����ô��ʼ�ȼ���1λ(8+1=9)
            //����ŵ�2^9=512��ʱ�򣬳����˵�ǰ����9���ܱ��ֵ����ֵ����ʱ����ı�žͱ�����10λ����ʾ
            //�Դ����ƣ�����ŵ�2^12��ʱ����Ϊ���Ϊ12,���ܼ�����չ�ˣ���Ҫ��2^12=4096��λ���ϲ���һ��ClearCode,
            //��ʾ�������󣬴�9λ����������
            clear_flg = false;
            n_bits = g_init_bits;
            //���nλ���ܱ��������ֵ(gifͼ���п�ʼһ��Ϊ3,5,9����maxcodeһ��Ϊ7,31,511)
            maxcode = MaxCode(n_bits);

            //��ʾ�����������¿�ʼ�����ֵ��ֵ��ˣ���ǰ�����б�����ϣ�
            //��ʼʹ���µı�ǡ������ż��Ĵ�С���ٱȽϺ����أ�
            //��˵��������Խ��ѹ����Խ�ߣ��Ҹ��˸о�̫����Ҳ�����þͺã���
            //��������Ŀ���Ҳ��ָ������
            //gif�涨��clearcode��ֵΪԭʼ��������ֳ����ܱ�����ֵ+1;
            //����ԭʼ���ݳ���Ϊ8,��clearcode=1<<(9-1)=256
            ClearCode = 1 << (init_bits - 1);
            //������־Ϊclearcode+1
            EOFCode = ClearCode + 1;
            //����ǽ��������
            free_ent = ClearCode + 2;
            //�������
            a_count = 0; // clear packet
            //��ͼ���л����һ������
            ent = NextPixel();

            hshift = 0;
            for (fcode = hsize; fcode < 65536; fcode *= 2)
                ++hshift;
            //����hash�뷶Χ
            hshift = 8 - hshift; // set hash code range bound

            hsize_reg = hsize;
            //����̶���С��hash�����ڴ洢��ǣ�����൱���ֵ�
            ResetCodeTable(hsize_reg); // clear hash table

            Output(ClearCode, outs);

        outer_loop: while ((c = NextPixel()) != EOF)
            {
                fcode = (c << maxbits) + ent;
                i = (c << hshift) ^ ent; // xor hashing
                //�ٺ�,С��,������,����ʶ��
                if (htab[i] == fcode)
                {
                    ent = codetab[i];
                    continue;
                }
                //��С��,������
                else if (htab[i] >= 0) // non-empty slot
                {
                    disp = hsize_reg - i; // secondary hash (after G. Knott)
                    if (i == 0)
                        disp = 1;
                    do
                    {
                        if ((i -= disp) < 0)
                            i += hsize_reg;

                        if (htab[i] == fcode)
                        {
                            ent = codetab[i];
                            goto outer_loop;
                        }
                    } while (htab[i] >= 0);
                }
                Output(ent, outs);
                //��������Կ���,ent����ǰ׺��prefix��,����ǰ���ڴ�����ַ���־���Ǻ�׺��suffix��
                ent = c;
                //�ж���ֹ�������Ƿ񳬹���ǰλ�����ܱ����ķ�Χ
                if (free_ent < maxmaxcode)
                {
                    //���û�г�
                    codetab[i] = free_ent++; // code -> hashtable
                    //hash�����潨����Ӧ����
                    htab[i] = fcode;
                }
                else
                    //˵�������˵�ǰ���ܱ����ķ�Χ,����ֵ�,��������
                    ClearTable(outs);
            }
            // Put out the final code.
            Output(ent, outs);
            Output(EOFCode, outs);
        }

        //----------------------------------------------------------------------------
        public void Encode(Stream os)
        {
            os.WriteByte(Convert.ToByte(initCodeSize)); // write "initial code size" byte
            //���ͼ��������ٸ�����
            remaining = imgW * imgH; // reset navigation variables
            //��ǰ�������������
            curPixel = 0;

            Compress(initCodeSize + 1, os); // compress and write the pixel data

            os.WriteByte(0); // write block terminator
        }

        // Flush the packet to disk, and reset the accumulator
        void Flush(Stream outs)
        {
            //if (a_count > 0)
            //{
            //    if (a_count > 255)
            //    {
            //        byte[] left_buffer = new byte[accum.Length - 255];
            //        accum.CopyTo(255, left_buffer.ConvertBytesToIBuffer(), 0, left_buffer.Length);
            //        outs.WriteByte(Convert.ToByte(left_buffer.Length));
            //        outs.Write(left_buffer, 0, left_buffer.Length);
            //    }
            //    else
            //    {
            //        outs.WriteByte(Convert.ToByte(a_count));
            //        outs.Write(accum, 0, a_count);
            //        //a_count = 0;
            //    }
            //    a_count = 0;
            //}

            if (a_count > 0)
            {
                outs.WriteByte(Convert.ToByte(a_count));
                outs.Write(accum, 0, a_count);
                a_count = 0;
            }
        }

        /// <summary>
        /// ���nλ�����ܱ��������ֵ
        /// </summary>
        /// <param name="n_bits">λ����һ�������n_bits = 9</param>
        /// <returns>���ֵ,����n_bits=8,�򷵻�ֵ��Ϊ2^8-1=255</returns>
        int MaxCode(int n_bits)
        {
            return (1 << n_bits) - 1;
        }

        //----------------------------------------------------------------------------
        // Return the next pixel from the image
        //----------------------------------------------------------------------------
        /// <summary>
        /// ��ͼ���л����һ������
        /// </summary>
        /// <returns></returns>
        private int NextPixel()
        {
            //��ʣ���ٸ�����û�д���
            //���û����,���ؽ�����־
            if (remaining == 0)
                return EOF;
            //��������һ��,����δ����������Ŀ-1
            --remaining;
            //��ǰ���������
            //int temp = curPixel + 1;
            int temp = curPixel - 1;
            //�����ǰ�������������ط�Χ֮��
            if (temp < pixAry.GetUpperBound(0))
            {
                //��һ������
                byte pix = pixAry[curPixel++];

                return pix & 0xff;
            }
            return 0xff;
        }

        /// <summary>
        /// ����ֵ������
        /// </summary>
        /// <param name="code">Ҫ�������</param>
        /// <param name="outs">�����</param>
        void Output(int code, Stream outs)
        {
            //�õ���ǰ��־λ���ܱ�ʾ������־ֵ
            cur_accum &= masks[cur_bits];

            if (cur_bits > 0)
                cur_accum |= (code << cur_bits);
            else
                //�����־λΪ0,�ͽ���ǰ���Ϊ������
                cur_accum = code;
            //��ǰ�ܱ�־������ֳ���(9-10-11-12-9-10��������������)
            cur_bits += n_bits;
            //�����ǰ��󳤶ȴ���8
            while (cur_bits >= 8)
            {
                //���������һ���ֽ�
                Add(Convert.ToByte((cur_accum & 0xff)), outs);
                //����ǰ�������8λ
                cur_accum >>= 8;
                cur_bits -= 8;
            }

            // If the next entry is going to be too big for the code size,
            // then increase it, if possible.
            if (free_ent > maxcode || clear_flg)
            {
                if (clear_flg)
                {
                    maxcode = MaxCode(n_bits = g_init_bits);
                    clear_flg = false;
                }
                else
                {
                    ++n_bits;
                    if (n_bits == maxbits)
                        maxcode = maxmaxcode;
                    else
                        maxcode = MaxCode(n_bits);
                }
            }

            if (code == EOFCode)
            {
                // At EOF, write the rest of the buffer.
                while (cur_bits > 0)
                {
                    Add((byte)(cur_accum & 0xff), outs);
                    cur_accum >>= 8;
                    cur_bits -= 8;
                }

                Flush(outs);
            }
        }
    }
}