namespace PicXWpf.Services;

public static class PicXGitHubConstants
{
    public const string DefaultRepositoryDescription = "PicX images hosting repository";

    public const string InitializeRepositoryCommitMessage =
        "Init repo via PicX-WPF (https://github.com/XPoet/picx)";

    public const string InitializeRepositoryReadmeContent = """
        # Welcome to use PicX

        [PicX](https://github.com/XPoet/picx) is a simple and powerful image hosting tool. It supports image hosting services via GitHub repository.

        PicX is completely open source, and you can use it for free.

        If you like it, please give it a star on [GitHub](https://github.com/XPoet/picx).
        """;
}
