using System;
using g3;

namespace gs
{
    public static class DMeshOpUtil
    {
        public static DMesh3 MakeFailureOutput(DMesh3 sourceMesh, bool bCopy = true)
        {
            DMesh3 failMesh = null;
            if (sourceMesh == null) {
                Sphere3Generator_NormalizedCube gen = new Sphere3Generator_NormalizedCube() { Radius = 25 };
                failMesh = gen.Generate().MakeDMesh();
                failMesh.EnableVertexColors(Colorf.VideoRed);
            } else if (bCopy) { 
                failMesh = new DMesh3(sourceMesh, false, MeshComponents.None);
                failMesh.EnableVertexColors(Colorf.VideoRed);
            }
            TagAsFailureMesh(failMesh);
            return failMesh;
        }


        public static void TagAsFailureMesh(DMesh3 mesh) {
            mesh.AttachMetadata("DMESHOP_FAILURE_MESH", new object());
        }

        public static void ClearFailureMesh(DMesh3 mesh) {
            mesh.RemoveMetadata("DMESHOP_FAILURE_MESH");
        }

        public static bool IsFailureMesh(DMesh3 mesh) {
            return mesh.FindMetadata("DMESHOP_FAILURE_MESH") != null;
        }

    }
}
