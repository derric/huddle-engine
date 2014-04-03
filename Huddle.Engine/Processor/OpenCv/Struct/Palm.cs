﻿using System;
using System.Collections.Generic;
using System.Drawing;
using GalaSoft.MvvmLight;
using Huddle.Engine.Processor.OpenCv.Filter;

namespace Huddle.Engine.Processor.OpenCv.Struct
{

    public class Palm : ObservableObject
    {
        #region private fields

        private readonly KalmanFilter _kalmanFilter = new KalmanFilter();

        #endregion

        #region properties

        #region Id

        /// <summary>
        /// The <see cref="Id" /> property's name.
        /// </summary>
        public const string IdPropertyName = "Id";

        private long _id = -1;

        /// <summary>
        /// Sets and gets the Id property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public long Id
        {
            get
            {
                return _id;
            }

            set
            {
                if (_id == value)
                {
                    return;
                }

                RaisePropertyChanging(IdPropertyName);
                _id = value;
                RaisePropertyChanged(IdPropertyName);
            }
        }

        #endregion

        #region Center

        /// <summary>
        /// The <see cref="Center" /> property's name.
        /// </summary>
        public const string CenterPropertyName = "Center";

        private Point _center = new Point();

        /// <summary>
        /// Sets and gets the Center property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public Point Center
        {
            get
            {
                return _center;
            }

            set
            {
                if (_center == value)
                {
                    return;
                }

                RaisePropertyChanging(CenterPropertyName);
                _center = value;
                RaisePropertyChanged(CenterPropertyName);
            }
        }

        #endregion

        #region LastUpdate

        /// <summary>
        /// The <see cref="LastUpdate" /> property's name.
        /// </summary>
        public const string LastUpdatePropertyName = "LastUpdate";

        private DateTime _lastUpdate;

        /// <summary>
        /// Sets and gets the LastUpdate property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public DateTime LastUpdate
        {
            get
            {
                return _lastUpdate;
            }

            set
            {
                if (_lastUpdate == value)
                {
                    return;
                }

                RaisePropertyChanging(LastUpdatePropertyName);
                _lastUpdate = value;
                RaisePropertyChanged(LastUpdatePropertyName);
            }
        }

        #endregion

        #endregion

        #region ctor

        public Palm(long id, Point center)
        {
            Id = id;
            Center = center;
        }

        #endregion

        #region PredictedCenter

        public Point PredictedCenter
        {
            get
            {
                return _kalmanFilter.GetPredictedPoint(Center);
            }
        }

        #endregion

        #region EstimatedCenter

        public Point EstimatedCenter
        {
            get
            {
                return _kalmanFilter.GetEstimatedPoint(Center);
            }
        }

        #endregion
    }
}
