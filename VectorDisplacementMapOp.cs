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
    /// This Op outputs an explicit VectorDisplacement map on the input mesh.
    /// User explicitly sets the displacement values via UpdateMap()
    /// </summary>
    public class VectorDisplacementMapOp : BaseSingleOutputModelingOperator, IVectorDisplacementSourceOp
    {
        protected VectorDisplacement Displacement;

        VectorDisplacement set_displacement;
        bool have_set_displacement = false;
        public virtual void UpdateMap(VectorDisplacement update)
        {
            lock (set_displacement) {
                set_displacement.Set(update);
                have_set_displacement = true;
            }
            base.invalidate();
        }
        
        /// <summary>
        /// Returns copy of current displacement map. thread-safe.
        /// </summary>
        public virtual VectorDisplacement GetMapCopy()
        {
            VectorDisplacement map = new VectorDisplacement();
            lock (Displacement) {
                map.Set(Displacement);
            }
            return map;
        }


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


        public VectorDisplacementMapOp(IMeshSourceOp meshSource = null)
        {
            Displacement = new VectorDisplacement();
            set_displacement = new VectorDisplacement();
            if ( meshSource != null )
                MeshSource = meshSource;
        }


        protected virtual void on_source_modified(ModelingOperator op)
        {
            base.invalidate();
        }



        public virtual void Update()
        {
            base.begin_update();

            if ( MeshSource == null )
                throw new Exception("VectorDisplacementMapOp: must set valid MeshSource to compute!");

            IMesh imesh = MeshSource.GetIMesh();
            if (imesh.HasVertexNormals == false)
                throw new Exception("VectorDisplacementMapOp: input mesh does not have surface normals...");
            if (imesh is DMesh3 == false)
                throw new Exception("VectorDisplacementMapOp: in current implementation, input mesh must be a DMesh3. Ugh.");
            DMesh3 mesh = imesh as DMesh3;

            lock(Displacement) {

                if (have_set_displacement) {
                    lock (set_displacement) {
                        if (set_displacement.Count == mesh.MaxVertexID)
                            Displacement.Set(set_displacement);
                        have_set_displacement = false;
                    }

                } else if ( Displacement.Count != mesh.MaxVertexID ) { 
                    Displacement.Clear();
                    Displacement.Resize(mesh.MaxVertexID);
                }
            }

            base.complete_update();
        }


        public IVectorDisplacement GetDisplacement() {
            if (base.requires_update())
                Update();

            return Displacement;
        }
    }








    /// <summary>
    /// Extension of VectorDisplacementMapOp that supports auto-generating the map.
    /// If .GenerateMap=true, then subclass must generate map
    /// Otherwise, we return the static map, which can be edited (ie via sculpting)
    /// </summary>
    public abstract class ParametricVectorDisplacementMapOp : VectorDisplacementMapOp
    {

        public ParametricVectorDisplacementMapOp(IMeshSourceOp meshSource = null) : base(meshSource)
        {
        }


        bool generate_map = true;
        public bool GenerateMap {
            get { return generate_map; }
            set { generate_map = value; base.invalidate(); }
        }

        public override void Update()
        {
            if (generate_map) {
                Update_GenerateMap();
            } else {
                base.Update();
            }
        }

        // subclass implements this to generate map
        protected abstract void Update_GenerateMap();
    }


}
