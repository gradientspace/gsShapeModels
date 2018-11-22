// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using g3;

namespace gs
{
    public class ExtrudeMeshOp : BaseDMeshSourceOp
    {
        DMesh3 ExtrudedMesh;

        IMeshSourceOp mesh_source;
        public IMeshSourceOp MeshSource {
            get { return mesh_source; }
            set {
                if (mesh_source != null)
                    mesh_source.OperatorModified -= on_input_modified;
                mesh_source = value;
                if (mesh_source != null)
                    mesh_source.OperatorModified += on_input_modified;
                base.invalidate();
            }
        }


        double extrude_dist = 0.1f;
        public double ExtrudeDistance {
            get { return extrude_dist; }
            set {
                if (extrude_dist != value) {
                    extrude_dist = value;
                    base.invalidate();
                }
            }
        }

        protected virtual void on_input_modified(ModelingOperator op) {
            base.invalidate();
        }




        public virtual void Update()
        {
            base.begin_update();

            if (MeshSource == null)
                throw new Exception("ExtrudeMeshOp: must set valid MeshSource to compute!");

            IMesh meshIn = MeshSource.GetIMesh();
            //ISpatial spatialIn = MeshSource.GetSpatial();

            ExtrudedMesh = new DMesh3(meshIn, MeshHints.None);

            MeshExtrudeMesh extrude = new MeshExtrudeMesh(ExtrudedMesh);
            extrude.ExtrudedPositionF = (v, n, vid) => {
                return v + extrude_dist * (Vector3d)n;
            };
            extrude.Extrude();

            ApplyModifiers(ExtrudedMesh);

            base.complete_update();
        }


        /*
         * IMeshSourceOp / DMeshSourceOp interface
         */

        public override IMesh GetIMesh()
        {
            if (base.requires_update())
                Update();
            return ExtrudedMesh;
        }

        public override DMesh3 GetDMeshUnsafe() {
            return (DMesh3)GetIMesh();
        }

        public override bool HasSpatial {
            get { return false; }
        }
        public override ISpatial GetSpatial() {
            return null;
        }

        public override DMesh3 ExtractDMesh()
        {
            Update();
            var result = ExtrudedMesh;
            ExtrudedMesh = null;
            base.result_consumed();
            return result;
        }

    }
}
