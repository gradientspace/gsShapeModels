using System;
using System.Collections.Generic;
using System.Linq;
using g3;

namespace gs
{



    public abstract class DGNode
    {
        public DependencyGraph Graph;

        public string Name;

        DGInputPortSet inputs = new DGInputPortSet();
        public DGInputPortSet Inputs {
            get { return inputs; }
            set {
                inputs = value;
                OnInputPortsModified(this, EventArgs.Empty);
            }
        }

        DGOutputPortSet outputs = new DGOutputPortSet();
        public DGOutputPortSet Outputs {
            get { return outputs; }
            set {
                outputs = value;
                OnOutputPortsModified(this, EventArgs.Empty);
            }
        }


        public DGNode()
        {
            inputs.OnPortsModified += OnInputPortsModified;
            outputs.OnPortsModified += OnOutputPortsModified;
        }

        public virtual void ForceEvaluate(DGArguments args)
        {
            RunRecompute(args);
        }


        // intermediary function you can override to implement caching, etc
        protected virtual void RunRecompute(DGArguments args)
        {
            Recompute(args);
        }


        protected abstract void Recompute(DGArguments args);



        protected virtual void OnInputPortsModified(object sender, EventArgs e)
        {
        }
        protected virtual void OnOutputPortsModified(object sender, EventArgs e)
        {
        }


    }





    public abstract class DGCachingNode : DGNode
    {
        struct CacheItem
        {
            public DGTimestamp timestamp;
            public object cache;
        }
        CacheItem[] InputCache;


        public DGCachingNode()
        {
        }


        protected virtual DGTimestamp InputsTimestamp {
            get {
                int sum = 0;
                for ( int i = 0; i < Inputs.Count; ++i ) {
                    DGTimestamp current = Inputs[i].Timestamp;
                    if (current == DGTimestamp.Invalid)
                        return DGTimestamp.Invalid;
                    sum += current.time;
                }
                return new DGTimestamp(sum);
            }
        }


        protected override void RunRecompute(DGArguments args)
        {
            System.Console.WriteLine("RunRecompute on node {0}", Name);

            bool found_dirty = false;
            for ( int i = 0; i < InputCache.Length; ++i ) {
                if ( InputCache[i].timestamp != Inputs[i].Timestamp ) {
                    found_dirty = true;
                    System.Console.WriteLine("    Found dirty at input {0} - type {1} - timestamp {2} vs cache {3}", i, Inputs[i].PortType.ToString(), Inputs[i].Timestamp.time, InputCache[i].timestamp.time);
                }
            }
            System.Console.WriteLine("  done dirty checks - result was {0}", found_dirty);

            if (found_dirty) {
                System.Console.WriteLine("  Recomputing on node {0}", Name);
                Recompute(args);
                System.Console.WriteLine("  Done on node {0}", Name);
            }
        }



        protected virtual T CachedValue<T>(int portIndex, DGArguments args)
        {
            if (InputCache[portIndex].timestamp != Inputs[portIndex].Timestamp) {
                InputCache[portIndex].cache = Inputs[portIndex].Value<object>(args);
                InputCache[portIndex].timestamp = Inputs[portIndex].Timestamp;
                System.Console.WriteLine("    {0}.CachedValue {1}:{2} - new timestamp {3}", Name, portIndex, Inputs[portIndex].PortType.ToString(), InputCache[portIndex].timestamp.time);
            }
            return (T)InputCache[portIndex].cache;
        }


        protected override void OnInputPortsModified(object sender, EventArgs e)
        {
            invalidate_cache();
        }
        //override protected void on_output_modified()
        //{
        //}



        protected virtual void invalidate_cache()
        {
            InputCache = new CacheItem[Inputs.Count];
            for (int i = 0; i < InputCache.Length; ++i) {
                InputCache[i].timestamp = DGTimestamp.Invalid;
            }
        }

    }


}
