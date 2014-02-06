﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using System.Xml.Serialization;
using GalaSoft.MvvmLight.Command;
using Tools.FlockingDevice.Tracking.Controls;
using Tools.FlockingDevice.Tracking.InkCanvas;
using Tools.FlockingDevice.Tracking.Model;
using Tools.FlockingDevice.Tracking.Processor;
using Tools.FlockingDevice.Tracking.Processor.OpenCv;
using Tools.FlockingDevice.Tracking.Properties;
using Tools.FlockingDevice.Tracking.Util;
using Application = System.Windows.Application;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Tools.FlockingDevice.Tracking.ViewModel
{
    public class PipelineViewModel : ProcessorViewModelBase<BaseProcessor>
    {
        #region prviate fields

        private readonly DataContractSerializer _serializer = new DataContractSerializer(typeof(Pipeline), null,
                                                                0x7FFF /*maxItemsInObjectGraph*/,
                                                                false /*ignoreExtensionDataObject*/,
                                                                true /*preserveObjectReferences : this is where the magic happens */,
                                                                null /*dataContractSurrogate*/);

        #endregion

        #region commands

        #region control commands

        public RelayCommand StartCommand { get; private set; }
        public RelayCommand StopCommand { get; private set; }
        public RelayCommand PauseCommand { get; private set; }
        public RelayCommand ResumeCommand { get; private set; }
        public RelayCommand SaveCommand { get; private set; }
        public RelayCommand LoadCommand { get; private set; }

        public RelayCommand<SenderAwareEventArgs> StrokeCollectedCommand { get; private set; }

        #endregion

        #region Drag & Drop commands

        public RelayCommand<MouseButtonEventArgs> DragInitiateCommand { get; private set; }
        public RelayCommand<SenderAwareEventArgs> DropProcessorCommand { get; private set; }

        #endregion

        #endregion

        #region properties

        #region Mode

        /// <summary>
        /// The <see cref="Mode" /> property's name.
        /// </summary>
        public const string ModePropertyName = "Mode";

        private PipelineMode _mode = PipelineMode.Stopped;

        /// <summary>
        /// Sets and gets the Mode property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public PipelineMode Mode
        {
            get
            {
                return _mode;
            }

            set
            {
                if (_mode == value)
                {
                    return;
                }

                RaisePropertyChanging(ModePropertyName);
                _mode = value;
                RaisePropertyChanged(ModePropertyName);
            }
        }

        #endregion

        #region ProcessorTypes

        /// <summary>
        /// The <see cref="ProcessorTypes" /> property's name.
        /// </summary>
        public const string ProcessorTypesPropertyName = "ProcessorTypes";

        private ObservableCollection<ViewTemplateAttribute> _processorTypes = new ObservableCollection<ViewTemplateAttribute>();

        /// <summary>
        /// Sets and gets the ProcessorTypes property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public ObservableCollection<ViewTemplateAttribute> ProcessorTypes
        {
            get
            {
                return _processorTypes;
            }

            set
            {
                if (_processorTypes == value)
                {
                    return;
                }

                RaisePropertyChanging(ProcessorTypesPropertyName);
                _processorTypes = value;
                RaisePropertyChanged(ProcessorTypesPropertyName);
            }
        }

        #endregion

        #endregion

        #region ctor

        public PipelineViewModel()
        {
            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
                //ProcessorTypes.Add(typeof(Basics));
                //ProcessorTypes.Add(typeof(CannyEdges));
                //ProcessorTypes.Add(typeof(FindContours));
                //ProcessorTypes.Add(typeof(BlobTracker));
            }
            else
            {
                var types = GetAttributesFromType<ViewTemplateAttribute, BaseProcessor>().ToArray();

                ProcessorTypes = new ObservableCollection<ViewTemplateAttribute>(types);
            }

            // exit hook to stop input source
            Application.Current.Exit += (s, e) =>
            {
                Stop();
                Save();
            };

            Model = new Pipeline();

            StartCommand = new RelayCommand(Start);
            StopCommand = new RelayCommand(Stop);
            PauseCommand = new RelayCommand(Pause);
            ResumeCommand = new RelayCommand(Resume);
            SaveCommand = new RelayCommand(Save);
            LoadCommand = new RelayCommand(Load);

            StrokeCollectedCommand = new RelayCommand<SenderAwareEventArgs>(OnStrokeCollected);

            DragInitiateCommand = new RelayCommand<MouseButtonEventArgs>(e =>
            {
                var element = e.Source as FrameworkElement;

                if (element == null) return;

                var viewTemplate = (ViewTemplateAttribute)element.DataContext;

                if (viewTemplate == null) return;

                // Initialize the drag & drop operation
                var format = typeof(BaseProcessor).IsAssignableFrom(viewTemplate.Type)
                    ? typeof(BaseProcessor).Name
                    : null;

                var dragData = new System.Windows.DataObject(format, viewTemplate.Type);

                DragDrop.DoDragDrop(element, dragData, System.Windows.DragDropEffects.Copy);
            });

            DropProcessorCommand = new RelayCommand<SenderAwareEventArgs>(e =>
            {
                var args = e.OriginalEventArgs as DragEventArgs;

                if (!args.Data.GetFormats().Any(f => Equals(typeof(BaseProcessor).Name, f))) return;
                var type = args.Data.GetData(typeof(BaseProcessor).Name) as Type;

                if (type == null)
                    return;

                var processor = (BaseProcessor)Activator.CreateInstance(type);

                // drop position
                var sender = e.Sender as FrameworkElement;
                if (sender != null)
                {
                    var position = args.GetPosition(sender);
                    processor.X = position.X;
                    processor.Y = position.Y;
                }

                var processorViewModel = new ProcessorViewModelBase<BaseProcessor>
                {
                    Model = processor,
                };

                Model.Children.Add(processor);
                Children.Add(processorViewModel);

                RaisePropertyChanged(ChildProcessorsPropertyName);

                IsDragOver = false;
            });

            Load();
        }

        #endregion

        public override void Start()
        {
            foreach (var processorViewModel in Children)
                processorViewModel.Start();

            Mode = PipelineMode.Started;
        }

        public override void Stop()
        {
            foreach (var processor in Children)
                processor.Stop();

            Mode = PipelineMode.Stopped;
        }

        public void Pause()
        {
            Mode = PipelineMode.Paused;

            throw new NotImplementedException();
        }

        public void Resume()
        {
            Start();

            throw new NotImplementedException();
        }

        #region private methods

        private void OnStrokeCollected(SenderAwareEventArgs e)
        {
            var inkCanvas = e.Sender as AdvancedInkCanvas;
            var eventArgs = e.OriginalEventArgs as StrokeEventArgs;

            var pg = PathGeometry.CreateFromGeometry(eventArgs.Stroke.GetGeometry());

            //if (eventArgs.Device == Device.Stylus)
            //{
            //    //throw new Exception("Analyze text");
            //    Console.WriteLine("STROKE {0}", AnalyzeStroke(eventArgs.Stroke));
            //    RecognizeGesture(pg);
            //}
            //else if (eventArgs.Device == Device.StylusInverted)
            //{
            //    var nodesToDelete = HitTestHelper.GetElementsInGeometry<UserControl>(pg, inkCanvas)
            //        .Select(view => view.DataContext)
            //        .OfType<QueryCanvasNodeViewModel>().ToArray();

            //    foreach (var vm in nodesToDelete)
            //    {
            //        var node = vm.Model as QueryCanvasNode;
            //        Debug.Assert(node != null, "node != null");

            //        Model.RemoveNode(node);
            //    }
            //}

            //var linksToDelete = HitTestHelper.GetElementsInGeometry<UserControl>(pg, inkCanvas)
            //    .Select(view => view.DataContext)
            //    .OfType<QueryCanvasLinkViewModel>().ToArray();

            ////TODO: add method to model, which is capable of deleting more than one link safely
            //foreach (var vm in linksToDelete)
            //{
            //    var link = vm.Model as QueryCanvasLink;
            //    Debug.Assert(link != null, "link != null");

            //    Model.DeleteLink(link);
            //}
        }

        private void Save()
        {
            var filename = Settings.Default.PipelineFilename;
            var tempFilename = String.Format("{0}.tmp", filename);

            try
            {
                using (var stream = new FileStream(tempFilename, FileMode.Create))
                {
                    var xmlTextWriter = XmlWriter.Create(stream, new XmlWriterSettings { NewLineOnAttributes = true, Indent = true });
                    _serializer.WriteObject(stream, Model);
                    //serializer.WriteObject(stream, new Test() { A = "ASDF" });
                }

                var bakFilename = String.Format("{0}.bak", Settings.Default.PipelineFilename);
                File.Replace(tempFilename, filename, bakFilename);
            }
            catch (Exception e)
            {
                ExceptionMessageBox.ShowException(e, String.Format(@"Could not save pipeline.{0}Exception Message: {1}", Environment.NewLine, e.Message));
            }
        }

        private void Load()
        {
            try
            {
                //var serializer = new XmlSerializer(typeof(Pipeline));
                using (var stream = new FileStream(Settings.Default.PipelineFilename, FileMode.Open))
                {
                    Model = _serializer.ReadObject(stream) as Pipeline ?? new Pipeline();

                    foreach (var processor in Model.Children)
                    {
                        var processorViewModel = BuildRecursiveViewModel(processor);
                        processorViewModel.ParentProcessor = this;
                        Children.Add(processorViewModel);
                    }
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(String.Format("Could not load pipeline. {0}.", e.Message), @"Pipeline Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static ProcessorViewModelBase<BaseProcessor> BuildRecursiveViewModel(BaseProcessor processor)
        {
            var processorViewModel = new ProcessorViewModelBase<BaseProcessor> { Model = processor };

            foreach (var child in processor.Children)
            {
                var childViewModel = BuildRecursiveViewModel(child);
                childViewModel.ParentProcessor = processorViewModel;
                processorViewModel.Children.Add(childViewModel);
            }

            return processorViewModel;
        }

        private static IEnumerable<TA> GetAttributesFromType<TA, T>()
            where TA : ViewTemplateAttribute
        {
            var types = from t in Assembly.GetExecutingAssembly().GetTypes()
                        where typeof(T).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface
                        select t;

            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<TA>();

                if (attr != null)
                {
                    attr.Type = type;
                    yield return attr;
                }
            }
        }

        #endregion
    }
}
