using System.Runtime.CompilerServices;

// 테스트 어셈블리에서 internal 유틸(BuildCapLoops, TriangulatePolygon 등)에 접근할 수 있게 함
[assembly: InternalsVisibleTo("Povet.MeshDestruction.Editor.Tests")]
