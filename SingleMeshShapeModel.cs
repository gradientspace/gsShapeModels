// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;

using g3;

namespace gs
{

    public interface IShapeModelMeshSource
    {
        DMesh3 SourceMesh { get; }
        ISpatial SourceSpatial { get; }
    }
    public delegate void MeshSourceModifiedEventHandler(IShapeModelMeshSource source);


    public interface IShapeModelMeshOutput
    {
        DMesh3 OutputMesh { get; }
        ISpatial OutputSpatial { get; }
    }
    public delegate void MeshOutputModifiedEventHander(IShapeModelMeshOutput model);



    public class SingleMeshShapeModel : IShapeModelMeshSource, IShapeModelMeshOutput
    {
        DMesh3 sourceMesh;
        ISpatial sourceSpatial;
        bool bExternalSource;

        DMesh3 outputMesh;
        ISpatial outputSpatial;
        bool bExternalOutput;


        /// <summary>
        /// Event will be called whenever our internal mesh changes
        /// </summary>
        public event MeshSourceModifiedEventHandler OnSourceMeshModified;

        /// <summary>
        /// Event should be called whenever 'output' of this model changes (to allow chaining models)
        /// </summary>
        public event MeshOutputModifiedEventHander OnOutputMeshModified;


        public SingleMeshShapeModel(DMesh3 meshIn, bool bCopy, ISpatial spatialIn = null)
        {
            ReplaceSourceMesh(meshIn, bCopy, spatialIn);
        }


        public virtual DMesh3 SourceMesh
        {
            get { return sourceMesh; }
        }
        public virtual ISpatial SourceSpatial
        {
            get { return sourceSpatial; }
        }


        public virtual DMesh3 OutputMesh {
            get { return outputMesh; }
        }
        public virtual ISpatial OutputSpatial {
            get { return outputSpatial; }
        }



        public virtual void ReplaceSourceMesh(DMesh3 meshIn, bool bCopy, ISpatial spatialIn = null)
        {
            bExternalSource = bCopy;
            if (bCopy)
                sourceMesh = new DMesh3(meshIn, true);
            else
                sourceMesh = meshIn;

            if ( spatialIn == null || bCopy ) {
                sourceSpatial = new DMeshAABBTree3(meshIn);
            } else {
                sourceSpatial = spatialIn;
            }

            OnSourceMeshModified?.Invoke(this);
        }



        public virtual DMesh3 DuplicateSourceMesh()
        {
            return new DMesh3(sourceMesh, bExternalSource);
        }




        protected virtual void ReplaceOutputMesh(DMesh3 meshIn, bool bCopy, ISpatial spatialIn = null)
        {
            bExternalOutput = bCopy;
            if (bCopy)
                outputMesh = new DMesh3(meshIn, true);
            else
                outputMesh = meshIn;

            if (spatialIn == null || bCopy) {
                outputSpatial = new DMeshAABBTree3(meshIn);
            } else {
                outputSpatial = spatialIn;
            }

            OnOutputMeshModified?.Invoke(this);
        }


        public virtual DMesh3 DuplicateOutputMesh()
        {
            return new DMesh3(outputMesh, bExternalOutput);
        }


    }



}

