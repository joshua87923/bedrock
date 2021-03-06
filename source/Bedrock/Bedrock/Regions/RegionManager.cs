using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Bedrock.Properties;
using Bedrock.Regions.Behaviors;
using Bedrock.Views;

namespace Bedrock.Regions
{
    /// <summary>
    /// This class is responsible for maintaining a collection of regions and attaching regions to controls. 
    /// </summary>
    /// <remarks>
    /// This class supplies the attached properties that can be used for simple region creation from XAML.
    /// </remarks>
    public class RegionManager : IRegionManager
    {
        private readonly IRegionBehaviorFactory _regionBehaviorFactory;
        private static readonly WeakDelegatesManager UpdatingRegionsListeners = new WeakDelegatesManager();

        /// <summary>
        /// Notification used by attached behaviors to update the region managers appropriatelly if needed to.
        /// </summary>
        /// <remarks>This event uses weak references to the event handler to prevent this static event of keeping the
        /// target element longer than expected.</remarks>
        public static event EventHandler UpdatingRegions
        {
            add { UpdatingRegionsListeners.AddListener(value); }
            remove { UpdatingRegionsListeners.RemoveListener(value); }
        }

        /// <summary>
        /// Notifies attached behaviors to update the region managers appropriatelly if needed to. 
        /// </summary>
        /// <remarks>
        /// This method is normally called internally, and there is usually no need to call this from user code.
        /// </remarks>
        public static void UpdateRegions()
        {
            try
            {
                UpdatingRegionsListeners.Raise(null, EventArgs.Empty);
            }
            catch (TargetInvocationException ex)
            {
                Exception rootException = ex.GetRootException();

                throw new UpdateRegionsException(string.Format(CultureInfo.CurrentCulture,
                    Resources.UpdateRegionException, rootException), ex.InnerException);
            }
        }

        public static void SetRegionManager(IView view, IRegionManager regionManager)
        {
            //            view.RegionManager = regionManager;
        }

        private readonly RegionCollection _regionCollection;

        /// <summary>
        /// Initializes a new instance of <see cref="RegionManager"/>.
        /// </summary>
        public RegionManager(IRegionBehaviorFactory regionBehaviorFactory)
        {
            _regionBehaviorFactory = regionBehaviorFactory;
            _regionCollection = new RegionCollection(this, regionBehaviorFactory);
        }

        /// <summary>
        /// Gets a collection of <see cref="IRegion"/> that identify each region by name. You can use this collection to add or remove regions to the current region manager.
        /// </summary>
        /// <value>A <see cref="IRegionCollection"/> with all the registered regions.</value>
        public IRegionCollection Regions
        {
            get { return _regionCollection; }
        }

        /// <summary>
        /// Creates a new region manager.
        /// </summary>
        /// <returns>A new region manager that can be used as a different scope from the current region manager.</returns>
        public IRegionManager CreateRegionManager()
        {
            return new RegionManager(_regionBehaviorFactory);
        }

        #region RegionCollection
        internal class RegionCollection : IRegionCollection
        {
            private readonly IRegionManager _regionManager;
            private readonly IRegionBehaviorFactory _regionBehaviorFactory;
            private readonly List<IRegion> _regions;

            public RegionCollection(IRegionManager regionManager, IRegionBehaviorFactory regionBehaviorFactory)
            {
                this._regionManager = regionManager;
                _regionBehaviorFactory = regionBehaviorFactory;
                this._regions = new List<IRegion>();
            }

            public event NotifyCollectionChangedEventHandler CollectionChanged;

            public IEnumerator<IRegion> GetEnumerator()
            {
                UpdateRegions();

                return this._regions.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public IRegion this[string regionName]
            {
                get
                {
                    UpdateRegions();

                    IRegion region = GetRegionByName(regionName);
                    if (region == null)
                    {
                        throw new KeyNotFoundException(string.Format(CultureInfo.CurrentUICulture, Resources.RegionNotInRegionManagerException, regionName));
                    }

                    return region;
                }
            }

            public void Add(IRegion region)
            {
                if (region == null) throw new ArgumentNullException("region");
                UpdateRegions();

                if (region.Name == null)
                {
                    throw new InvalidOperationException(Resources.RegionNameCannotBeEmptyException);
                }

                if (this.GetRegionByName(region.Name) != null)
                {
                    throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                                                              Resources.RegionNameExistsException, region.Name));
                }

                this._regions.Add(region);
                region.RegionManager = this._regionManager;
                RegisterDefaultBehavior(region);
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, region, 0));
            }

            public bool Remove(string regionName)
            {
                UpdateRegions();

                bool removed = false;

                IRegion region = GetRegionByName(regionName);
                if (region != null)
                {
                    removed = true;
                    this._regions.Remove(region);
                    region.RegionManager = null;

                    this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, region, 0));
                }

                return removed;
            }

            public bool ContainsRegionWithName(string regionName)
            {
                UpdateRegions();

                return GetRegionByName(regionName) != null;
            }

            public IRegion GetRegionByName(string regionName)
            {
                return this._regions.FirstOrDefault(r => r.Name == regionName);
            }

            private void OnCollectionChanged(NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
            {
                var handler = this.CollectionChanged;

                if (handler != null)
                {
                    handler(this, notifyCollectionChangedEventArgs);
                }
            }

            private void RegisterDefaultBehavior(IRegion region)
            {
                var enumerator = _regionBehaviorFactory.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var behaviorKey = enumerator.Current;
                    if (!region.Behaviors.ContainsKey(behaviorKey))
                    {
                        var behavior = _regionBehaviorFactory.CreateFromKey(behaviorKey);
                        region.Behaviors.Add(behaviorKey, behavior);
                    }
                }
            }
        }
        #endregion
    }
}
