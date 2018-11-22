// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace gs
{
    public delegate void OpCollectionModifiedEvent(ModelingOpCollection collection);
    public delegate void OpCollectionItemModifiedEvent(ModelingOpCollection collection, ModelingOperator item);


    public interface ModelingOpCollection
    {
        event OpCollectionModifiedEvent CollectionModified;
        event OpCollectionItemModifiedEvent CollectionItemModified;
    }


    public abstract class BaseModelingOpCollection : ModelingOpCollection
    {
        public event OpCollectionModifiedEvent CollectionModified;
        public event OpCollectionItemModifiedEvent CollectionItemModified;

        virtual protected void PostOnCollectionModified() {
            var safeevent = CollectionModified;
            if (safeevent != null)
                safeevent(this);
        }

        virtual protected void PostOnCollectionItemModified(ModelingOperator item) {
            var safeevent = CollectionItemModified;
            if (safeevent != null)
                safeevent(this, item);
        }
    }



    public class ShapeModelingOpList<T> : BaseModelingOpCollection, IEnumerable<T> where T : ModelingOperator
    {
        List<T> items;

        public ShapeModelingOpList() {
            items = new List<T>();
        }


        public int Count {
            get { return items.Count; }
        }

        public T this[int i] {
            get { return items[i]; }
        }

        public IReadOnlyList<T> Operators {
            get { return items; }
        }

        public IEnumerator<T> GetEnumerator() {
            return items.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }


        public void Append(T op)
        {
            items.Add(op);
            op.OperatorModified += on_collection_item_modified;
            on_collection_modified();
        }


        public bool Remove(T op)
        {
            bool found = items.Remove(op);
            if (found == false)
                throw new Exception("ShapeModelingOpList.Remove: item " + op.ToString() + " was not found!");
            op.OperatorModified -= on_collection_item_modified;
            on_collection_modified();
            return true;
        }

        
        void on_collection_modified()
        {
            PostOnCollectionModified();
        }

        void on_collection_item_modified(ModelingOperator item)
        {
            PostOnCollectionItemModified(item);
        }

    }
}
