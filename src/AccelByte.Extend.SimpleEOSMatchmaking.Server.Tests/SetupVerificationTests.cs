using System;
using Xunit;
using FsCheck;
using FsCheck.Xunit;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests
{
    /// <summary>
    /// Verification tests to ensure test project setup is correct
    /// </summary>
    public class SetupVerificationTests
    {
        [Fact]
        public void XUnit_IsConfiguredCorrectly()
        {
            // Arrange & Act
            var result = true;

            // Assert
            Assert.True(result);
        }

        [Property]
        public Property FsCheck_IsConfiguredCorrectly(int x)
        {
            // Property: Adding zero to any integer returns the same integer
            return (x + 0 == x).ToProperty();
        }
    }
}
