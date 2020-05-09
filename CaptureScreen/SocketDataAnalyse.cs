using System.Collections.Generic;
using SpaceCG.Generic;

namespace CaptureScreen
{
    /// <summary>
    /// Socket 数据分析对象
    /// </summary>
    /// <typeparam name="TChannelKey"></typeparam>
    public class SocketDataAnalyse<TChannelKey> : TerminatorDataAnalysePattern<TChannelKey, bool>
    {
        /// <summary>
        /// 以 0D 0A 分隔数据
        /// </summary>
        public SocketDataAnalyse() : base(terminator: new byte[2] { 0x0D, 0x0A }) // 指定结束符为\r\n
        {
        }

        /// <inheritdoc/>
        protected override bool ConvertResultType(List<byte> data)
        {
            return data[0] != 0x00;
        }
    }
}
