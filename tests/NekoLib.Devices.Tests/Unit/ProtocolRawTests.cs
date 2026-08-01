using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using NekoLib.Devices.Core.Abstractions;
using NekoLib.Devices.Core.Protocols;
using Xunit;

namespace NekoLib.Devices.Tests.Unit
{
    public class ProtocolRawTests
    {
        [Fact]
        public void BuildCommand_RawBytes_ReturnsSameBuffer()
        {
            var protocol = new ProtocolRaw();
            var bytes = new byte[] { 0x01, 0x02, 0x03 };
            var op = new HardwareOperation
            {
                Args = new Dictionary<string, object> { ["RawBytes"] = bytes }
            };

            var result = protocol.BuildCommand(op);

            Assert.Same(bytes, result);
        }

        [Fact]
        public void BuildCommand_RawText_ReturnsAsciiBytes()
        {
            var protocol = new ProtocolRaw();
            var op = new HardwareOperation
            {
                Args = new Dictionary<string, object> { ["RawText"] = "PING" }
            };

            var result = protocol.BuildCommand(op);

            Assert.Equal(Encoding.ASCII.GetBytes("PING"), result);
        }

        [Fact]
        public void BuildCommand_MissingRawArguments_Throws()
        {
            var protocol = new ProtocolRaw();
            var op = new HardwareOperation();

            var ex = Assert.Throws<InvalidOperationException>(() => protocol.BuildCommand(op));

            Assert.Contains("RawBytes", ex.Message);
            Assert.Contains("RawText", ex.Message);
        }

        [Fact]
        public void BuildCommand_InvalidRawBytesType_Throws()
        {
            var protocol = new ProtocolRaw();
            var op = new HardwareOperation
            {
                Args = new Dictionary<string, object> { ["RawBytes"] = "not bytes" }
            };

            var ex = Assert.Throws<ArgumentException>(() => protocol.BuildCommand(op));

            Assert.Contains("RawBytes", ex.Message);
        }

        [Fact]
        public void BuildCommand_InvalidRawTextType_Throws()
        {
            var protocol = new ProtocolRaw();
            var op = new HardwareOperation
            {
                Args = new Dictionary<string, object> { ["RawText"] = 42 }
            };

            var ex = Assert.Throws<ArgumentException>(() => protocol.BuildCommand(op));

            Assert.Contains("RawText", ex.Message);
        }

        [Fact]
        public void ParseResponse_NullReply_ReturnsNoResponse()
        {
            var protocol = new ProtocolRaw();

            var response = protocol.ParseResponse(null, new HardwareOperation());

            Assert.False(response.Success);
            Assert.Equal("NoResponse", response.Status);
            Assert.Null(response.RawBytes);
            Assert.Null(response.RawText);
        }

        [Fact]
        public void ParseResponse_Reply_ReturnsRawBytesAndAsciiText()
        {
            var protocol = new ProtocolRaw();
            var reply = Encoding.ASCII.GetBytes("OK");

            var response = protocol.ParseResponse(reply, new HardwareOperation());

            Assert.True(response.Success);
            Assert.Equal("Ok", response.Status);
            Assert.Same(reply, response.RawBytes);
            Assert.Equal("OK", response.RawText);
        }

        [Fact]
        public void Constructor_CustomConfigAndEncoding_AreUsed()
        {
            var config = new SerialConfig
            {
                BaudRate = 9600,
                Parity = Parity.Even,
                DataBits = 7,
                StopBits = StopBits.One,
                PortName = @"\\.\pipe\pcb-a"
            };
            var protocol = new ProtocolRaw(config, Encoding.GetEncoding(28591));
            var op = new HardwareOperation
            {
                Args = new Dictionary<string, object>
                {
                    ["RawText"] = "AÇÃO"
                }
            };

            var command = protocol.BuildCommand(op);
            var response = protocol.ParseResponse(command, op);

            Assert.Same(config, protocol.PortConfig);
            Assert.Equal(@"\\.\pipe\pcb-a", protocol.PortConfig.PortName);
            Assert.Equal(new byte[] { 0x41, 0xC7, 0xC3, 0x4F }, command);
            Assert.Equal("AÇÃO", response.RawText);
        }
    }
}
