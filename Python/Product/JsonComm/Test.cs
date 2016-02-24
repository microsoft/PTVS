using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Cdp;

namespace Microsoft.PythonTools.Analysis.Communication {
    class TestRequest : Request<TestResponse> {
        public const string Command = "test";
        public string foo;

        public override string command => Command;
    }

    class TestResponse : Response {
        public string data;
    }

    class TestResponse2 : Response {
        public string data, foo;
    }

    class Test {
        public static void Main(string[] args) {
            TestWorker().Wait();
        }

        public async static Task TestWorker() {
            MemoryStream inp = new EchoStream();
            MemoryStream outp = new EchoStream();

            Console.WriteLine("Starting server");

            Connection server = StartServer(inp, outp);

            Console.WriteLine("Starting client");
            var client = StartClient(inp, outp);

            Console.WriteLine("Sending request");
            var resp = await client.SendRequestAsync(new TestRequest() { foo = "hi" });

            Console.WriteLine("Response received " + resp);

            resp = await client.SendRequestAsync(new TestRequest() { foo = "test" });
            Console.WriteLine("Response received " + resp.data);
            Console.WriteLine("Response received ");
        }

        private static Connection StartClient(MemoryStream inp, MemoryStream outp) {
            var res = new Connection(inp, outp);
            res.StartProcessing();
            return res;
        }

        private static Connection StartServer(MemoryStream inp, MemoryStream outp) {
            var res = new Connection(outp, inp, requestArgs => {
                Console.WriteLine("Request received");
                var request = requestArgs.Request;

                TestRequest req = (TestRequest)request;
                if (req.foo == "hi") {
                    return Task.FromResult((Response)new TestResponse() { data = "yo" });
                }
                return Task.FromResult((Response)new TestResponse2() { data = "yo", foo = "hi" });
            },
            new Dictionary<string, Type>() {
                { "request.test", typeof(TestRequest) }
            });
            res.StartProcessing();
            return res;
        }

        public class EchoStream : MemoryStream {
            private readonly ManualResetEvent _DataReady = new ManualResetEvent(false);
            private readonly ConcurrentQueue<byte[]> _Buffers = new ConcurrentQueue<byte[]>();

            public bool DataAvailable { get { return !_Buffers.IsEmpty; } }

            public override void Write(byte[] buffer, int offset, int count) {
                _Buffers.Enqueue(buffer);
                _DataReady.Set();
            }

            public override int Read(byte[] buffer, int offset, int count) {
                _DataReady.WaitOne();

                byte[] lBuffer;

                if (!_Buffers.TryDequeue(out lBuffer)) {
                    _DataReady.Reset();
                    return -1;
                }

                if (!DataAvailable)
                    _DataReady.Reset();

                Array.Copy(lBuffer, buffer, lBuffer.Length);
                return lBuffer.Length;
            }
        }
    }
}
