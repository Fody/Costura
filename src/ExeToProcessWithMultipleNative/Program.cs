namespace ExeToProcessWithMultipleNative
{
    using System;

    public class Program
    {
        public static int Main()
        {
            // you can pass ANY valid library here, it's only important that kafka runs it's internal setup :-)
            Confluent.Kafka.Library.Load("librdkafka.dll");

            QueueHelper.SendMessage("test", "message1");

            Console.WriteLine(Guid.NewGuid().ToString());
            QueueHelper.SendMessage("test", "message2");

            return 42;
        }

        public int Test()
        {
            return Main();
        }
    }
}
