using HashingDemo.Logic;
using System.Text;
using Xunit;

namespace UnitTests;

public class PasswordHasherTests
{
    [Fact]
    public void ComputePasswordHash_ChangingSalt_ChangesHash()
    {
        // Arrange
        var password = "mysecretpassword";
        var salt1 = PasswordHasher.GenerateSalt();
        var salt2 = PasswordHasher.GenerateSalt();
        var iterations = 100;

        // Act
        var hash1 = PasswordHasher.ComputePasswordHash(password, salt1, iterations);
        var hash2 = PasswordHasher.ComputePasswordHash(password, salt2, iterations);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputePasswordHash_ChangingIterations_ChangesHash()
    {
        // Arrange
        var password = "mysecretpassword";
        var salt = PasswordHasher.GenerateSalt();
        var iterations1 = 100;
        var iterations2 = 200;

        // Act
        var hash1 = PasswordHasher.ComputePasswordHash(password, salt, iterations1);
        var hash2 = PasswordHasher.ComputePasswordHash(password, salt, iterations2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ConstantTimeEquals_ReturnsTrue_ForEqualHashes()
    {
        // Arrange
        var hash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        // Act
        var result = PasswordHasher.ConstantTimeEquals(hash, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ConstantTimeEquals_ReturnsFalse_ForDifferentHashes()
    {
        // Arrange
        var hash1 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        var hash2 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        // Act
        var result = PasswordHasher.ConstantTimeEquals(hash1, hash2);

        // Assert
        Assert.False(result);
    }
}
