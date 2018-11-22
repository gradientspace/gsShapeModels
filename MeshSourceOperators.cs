// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using g3;

namespace gs
{


    public class WrapIMeshSourceOp : BaseModelingOperator, IMeshSourceOp
    {
        // provide a function that returns source mesh
        public Func<IMesh> MeshSourceF;
        public Func<ISpatial> SpatialSourceF;

        public IMesh GetIMesh() {
            return MeshSourceF();
        }

        public bool HasSpatial {
            get { return SpatialSourceF != null; }
        }

        public ISpatial GetSpatial()
        {
            return SpatialSourceF();
        }

        // connect this to modified event of your own
        public void RelayExternalModifiedEvent() {
            PostOnOperatorModified();
        }
    }



    public class WrapDMeshSourceOp : BaseModelingOperator, DMeshSourceOp
    {
        // provide a function that returns source mesh
        public Func<DMesh3> MeshSourceF;
        public Func<DMeshAABBTree3> SpatialSourceF;

        public IMesh GetIMesh() {
            return MeshSourceF();
        }
        public DMesh3 GetDMeshUnsafe() {
            return MeshSourceF();
        }
        public DMesh3 ExtractDMesh() {
            return new DMesh3(MeshSourceF());
        }

        public bool HasSpatial {
            get { return SpatialSourceF() != null; }
        }
        public ISpatial GetSpatial() {
            return SpatialSourceF();
        }
    }






    public class ConstantMeshSourceOp : BaseModelingOperator, DMeshSourceOp
    {
        DMesh3 mesh;
        DMeshAABBTree3 spatial;

        public ConstantMeshSourceOp()
        {
        }

        public ConstantMeshSourceOp(DMesh3 meshIn, bool buildSpatial, bool bTakeOwnership)
        {
            mesh = (bTakeOwnership) ? meshIn : new DMesh3(meshIn);
            if (buildSpatial) {
                spatial = new DMeshAABBTree3(mesh);
                spatial.Build();
            }
        }

        public void SetMesh(DMesh3 meshIn, bool buildSpatial, bool bTakeOwnership)
        {
            mesh = (bTakeOwnership) ? meshIn : new DMesh3(meshIn);

            spatial = null;
            if (buildSpatial) {
                spatial = new DMeshAABBTree3(mesh);
                spatial.Build();
            }

            PostOnOperatorModified();
        }


        public IMesh GetIMesh()
        {
            return mesh;
        }

        public DMesh3 GetDMeshUnsafe()
        {
            return mesh;
        }
        public DMesh3 ExtractDMesh()
        {
            return new DMesh3(mesh);
        }


        public bool HasSpatial {
            get { return spatial != null; }
        }

        public ISpatial GetSpatial()
        {
            return spatial;
        }
    }








    public class ShapeModelOutputMeshSourceOp : BaseModelingOperator, DMeshSourceOp
    {
        SingleMeshShapeModel model;

        public ShapeModelOutputMeshSourceOp(SingleMeshShapeModel model)
        {
            this.model = model;
            model.OnOutputMeshModified += Model_OnOutputMeshModified;
        }

        private void Model_OnOutputMeshModified(IShapeModelMeshOutput model)
        {
            PostOnOperatorModified();
        }


        public IMesh GetIMesh()
        {
            return model.OutputMesh;
        }

        public bool HasSpatial {
            get { return model.OutputSpatial != null; }
        }

        public ISpatial GetSpatial()
        {
            return model.OutputSpatial;
        }


        public DMesh3 GetDMeshUnsafe()
        {
            return model.OutputMesh;
        }

        public DMesh3 ExtractDMesh()
        {
            return model.DuplicateOutputMesh();
        }

    }



}
