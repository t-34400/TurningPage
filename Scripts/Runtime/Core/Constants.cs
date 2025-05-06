#nullable enable

namespace TurningPage
{
    public static class Constants
    {
        public const string PACKAGE_DIR = "Assets/TurningPage/";
        public const string COMPUTE_SHADER_DIR = PACKAGE_DIR + "ComputeShaders/Core/";
        public const string INITIALIZER_SHADER_FILENAME = COMPUTE_SHADER_DIR + "InitialilzeVertices.compute";
        public const string UPDATER_SHADER_FILENAME = COMPUTE_SHADER_DIR + "ProjectOnCone.compute";
        public const string VERTEX_SEARCHER_SHADER_FILENAME = COMPUTE_SHADER_DIR + "SearchNearestVertex.compute";
    }
}