namespace ExeToProcessWithMultipleNative
{
    using System.Net;

    using Confluent.Kafka;

    public static class QueueHelper
    {
        private const string QueueAddressesList = "kafka:9200";

        public static void SendMessage(string topic, string message)
        {
            var producer = new ProducerBuilder<Null, string>(new ProducerConfig
            {
                ClientId = Dns.GetHostName(),
                BootstrapServers = QueueAddressesList
            }).Build();
            producer.Produce(topic, new Message<Null, string> { Value = message });
        }
    }
}
