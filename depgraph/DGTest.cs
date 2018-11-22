using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public static class DGTest
    {
        public static void test(Action<object> printf)
        {
            StringOutPort inPathPort = new StringOutPort() { Value = "c:\\scratch\\bunny_open.obj" };
            StringOutPort outPathPort = new StringOutPort() { Value = "c:\\scratch\\__generated_mesh.obj" };
            DoubleOutPort distPort = new DoubleOutPort() { Value = 2.0f };

            ReadMeshNode readNode = new ReadMeshNode() { Name = "readMesh" };
            readNode.Inputs[0].ConnectToSource(inPathPort);

            MeshNormalOffsetNode offsetNode = new MeshNormalOffsetNode() { Name = "offsetMesh" };
            offsetNode.Inputs[0].ConnectToSource(readNode.Outputs[0]);
            offsetNode.Inputs[1].ConnectToSource(distPort);

            WriteMeshNode writeNode = new WriteMeshNode() { Name = "writeMesh" };
            writeNode.Inputs[0].ConnectToSource(outPathPort);
            writeNode.Inputs[1].ConnectToSource(offsetNode.Outputs[0]);

            writeNode.ForceEvaluate(DGArguments.Empty);

            distPort.Value = 3.0f;
            outPathPort.Value = "c:\\scratch\\__generated_mesh_2.obj";
            writeNode.ForceEvaluate(DGArguments.Empty);

        }

    }
}
