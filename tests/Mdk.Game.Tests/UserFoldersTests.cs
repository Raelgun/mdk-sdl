using Mdk.Game.Flow;

namespace Mdk.Game.Tests;

/// <summary>Settings and saves next to the executable, or the user's data folder when it's protected (#9).</summary>
public class UserFoldersTests
{
    [Fact]
    public void WritableFolderBesideTheProgramIsUsed()
    {
        var beside = Directory.CreateTempSubdirectory().FullName;
        var personal = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Assert.Equal(beside, UserFolders.Choose(beside, personal));
        Assert.False(Directory.Exists(personal));
        Assert.Empty(Directory.GetFiles(beside));
    }

    [Fact]
    public void ProtectedFolderFallsBackToThePersonalOne()
    {
        // A file where the folder should be: nothing can be written "in" it.
        var beside = Path.GetTempFileName();
        var personal = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Assert.Equal(personal, UserFolders.Choose(beside, personal));
        Assert.True(Directory.Exists(personal));
    }

    [Fact]
    public void PersonalFolderIsNamedAfterTheProgram() =>
        Assert.Equal("mdk-sdl", Path.GetFileName(UserFolders.Personal()));
}
