using Ctrl2MqttClient;
using System;
using Xunit;

namespace Ctrl2MqttBridgeClientTests
{
    public class BridgeSettingsHelperTests
    {
        [Fact]
        public void GetBridgeTopicTest()
        {
            string bridgeTopic = BridgeSettingsHelper.GetBridgeTopic();
            Assert.False(String.IsNullOrWhiteSpace(bridgeTopic));
        }
        [Fact]
        public void GetBridgeCredentialsTest()
        {
            var bridgeCredentials = BridgeSettingsHelper.GetBridgeCredentials();
            Assert.NotNull(bridgeCredentials);
            Assert.False(String.IsNullOrWhiteSpace(bridgeCredentials.Username));
            Assert.False(String.IsNullOrWhiteSpace(bridgeCredentials.Password));

        }
    }
}
