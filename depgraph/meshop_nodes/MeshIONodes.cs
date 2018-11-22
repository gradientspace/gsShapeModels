using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using g3;

namespace gs
{
    public class ReadMeshNode : DGCachingNode
    {
        public DMesh3 ReadMesh;

        public ReadMeshNode()
        {
            Inputs.Add(new StringInPort());

            Outputs.Add(new ExternalDMesh3OutPort() {
                MeshSourceF = (args) => { return GetReadMesh(args); },
                //TimestampSourceF = () => { return new DGTimestamp(ReadMesh.Timestamp); }
                TimestampSourceF = () => { return InputsTimestamp; }
            });

            Outputs.Add(new DMesh3OutPort());
        }


        protected virtual DMesh3 GetReadMesh(DGArguments args)
        {
            RunRecompute(args);
            return ReadMesh;
        }


        protected override void Recompute(DGArguments args)
        {
            System.Console.WriteLine("Reading file...");

            ReadMesh = new DMesh3();

            string path = CachedValue<string>(0, args);
            if (!File.Exists(path)) {
                return;
            }

            DMesh3Builder builder = new DMesh3Builder();
            StandardMeshReader reader = new StandardMeshReader();
            reader.MeshBuilder = builder;
            IOReadResult result = reader.Read(path, ReadOptions.Defaults);
            if ( result.code != IOCode.Ok ) {
                return;
            }

            ReadMesh = builder.Meshes[0];
        }

    }





    public class WriteMeshNode : DGNode
    {

        public WriteMeshNode()
        {
            Inputs.Add(new StringInPort());
            Inputs.Add(new DMesh3InPort());
        }


        protected override void Recompute(DGArguments args)
        {
            string path = Inputs[0].Value<string>(args);

            DMesh3 mesh = Inputs[1].Value<DMesh3>(args);

            StandardMeshWriter writer = new StandardMeshWriter();
            IOWriteResult result = 
                writer.Write(path, new List<WriteMesh>() { new WriteMesh(mesh) }, WriteOptions.Defaults);
            if (result.code != IOCode.Ok) {
                // what??
            }
        }

    }

}
