using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Util
{
    public class UpdatableList<T> : IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>
    {
        List<T> list = new List<T>();
        List<T> addList = new List<T>();
        List<T> removeList = new List<T>();

        public int Count => list.Count;

        public bool Contains(T t)
        {
            if (removeList.Contains(t))
                return false;
            if (addList.Contains(t) || list.Contains(t))
                return true;
            return false;
        }


        public void Add(T t)
        {
            addList.Add(t);
        }

        public void Remove(T t)
        {
            removeList.Add(t);
        }


        public void Update()
        {
            list.AddRange(addList);
            removeList.ForEach(item => list.Remove(item));
            addList.Clear();
            removeList.Clear();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }
    }
}
