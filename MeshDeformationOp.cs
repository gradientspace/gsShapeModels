// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    /// <summary>
    /// Generic Op that applies a deformation function to input mesh 
    /// </summary>
    public class MeshDeformationOp : BaseSingleOutputModelingOperator, DMeshSourceOp
    {
        DMesh3 DisplacedMesh;

        IMeshSourceOp mesh_source;
        public IMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_source_modified;
                mesh_source = value;
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_source_modified;
                base.invalidate();
            }
        }


        Func<Vector3d, Vector3f, int, Vector3d> deformF;
        public Func<Vector3d, Vector3f, int, Vector3d> DeformFunction {
            get { return deformF; }
            set { deformF = value; base.invalidate(); }
        }


        protected virtual void on_source_modified(ModelingOperator op) {
            base.invalidate();
        }


        public virtual void Update()
        {
            base.begin_update();

            if (MeshSource == null)
                throw new Exception("MeshDeformationOp: must set valid MeshSource to compute!");

            IMesh meshIn = MeshSource.GetIMesh();

            DMesh3 mesh = new DMesh3(meshIn, MeshHints.None);
            MeshNormals.QuickCompute(mesh);

            foreach (int vid in mesh.VertexIndices() ) {
                Vector3d v = mesh.GetVertex(vid);
                Vector3f n = mesh.GetVertexNormal(vid);
                Vector3d newPos = deformF(v, n, vid);
                mesh.SetVertex(vid, newPos);
            }

            MeshNormals.QuickCompute(mesh);

            DisplacedMesh = mesh;

            base.complete_update();
        }


        public virtual IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return DisplacedMesh;
        }

        public virtual DMesh3 GetDMeshUnsafe() {
            return (DMesh3)GetIMesh();
        }

        public bool HasSpatial {
            get { return false; }
        }
        public ISpatial GetSpatial() {
            return null;
        }

        public virtual DMesh3 ExtractDMesh()
        {
            Update();
            var result = DisplacedMesh;
            DisplacedMesh = null;
            result_consumed();
            return result;
        }

    }
}
