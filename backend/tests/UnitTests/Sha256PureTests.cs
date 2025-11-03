using HashingDemo.Logic;
using System.Text;
using Xunit;

namespace UnitTests;

public class Sha256PureTests
{
    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq", "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1")]
    public void ComputeHash_ReturnsCorrectHash_ForKnownTestVectors(string input, string expectedHash)
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes(input);

        // Act
        var hashBytes = Sha256Pure.ComputeHash(data);
        var actualHash = PasswordHasher.ToHexString(hashBytes);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }
}
