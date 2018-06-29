using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;

namespace FastMPI
{
    /// <summary>
    /// Interaction logic for MainControl.xaml
    /// </summary>
    /// 


    public partial class MainControl : UserControl
    {

        public MainControl()
        {
            InitializeComponent();
            PrepareBinding();
        }


        VMS.TPS.Script script = new VMS.TPS.Script();


        private void PrepareBinding()
        {
            lstFields.ItemsSource = VMS.TPS.Script.StaticCollCoursePlan;
        }

        private void PrepareBindingStructures()
        {
            script.PrepareStructures();
            lstStructures.ItemsSource = VMS.TPS.Script.StaticCollStructureTarget;
        }


        private void PrepareBindingResults()
        {
            bool check = chbxChangeWeights.IsChecked ?? false;   // linia wstawiona, bo własciwosc IsChecked moze miec trzy stany True, False, Null
                                                                 // (tej ostatniej nie moge przekazywac do CalculateResults)
            script.CalculateResults(check);
            ICollectionView view = CollectionViewSource.GetDefaultView(VMS.TPS.Script.StaticCollResult);
            view.SortDescriptions.Add(new SortDescription("IndexResult", ListSortDirection.Descending)); // 
            MessageBox.Show("Calculations done");                                                 
            lstResults.ItemsSource = view;                                                        
        }

        private void PrepareBindingCNResults()
        {
            script.CalculateCNResults();
            ICollectionView view = CollectionViewSource.GetDefaultView(VMS.TPS.Script.StaticCollResult);
            view.SortDescriptions.Add(new SortDescription("IndexResult", ListSortDirection.Descending));
            MessageBox.Show("Calculations done");
            lstResults.ItemsSource = view;
        }

        private void PrepareBindingGIResults()
        {
            script.CalculateGIResults();
            ICollectionView view = CollectionViewSource.GetDefaultView(VMS.TPS.Script.StaticCollResult);
            view.SortDescriptions.Add(new SortDescription("IndexResult", ListSortDirection.Ascending));
            MessageBox.Show("Calculations done");
            lstResults.ItemsSource = view;
        }


        private void GIRadioButtonUnchecked(object sender, RoutedEventArgs e)
        {
            VMS.TPS.Script.StaticCollResult = null;
            lstResults.ItemsSource = null;
            //PrepareBindingGIResults();
        }

        private void CNRadioButtonUnchecked(object sender, RoutedEventArgs e)
        {
            VMS.TPS.Script.StaticCollResult = null;
            lstResults.ItemsSource = null;
            //PrepareBindingGIResults();
        }

        private void RPIRadioButtonUnchecked(object sender, RoutedEventArgs e)
        {
            VMS.TPS.Script.StaticCollStructureTarget = null;
            lstStructures.ItemsSource = null;
            VMS.TPS.Script.StaticCollResult = null;
            lstResults.ItemsSource = null;
            //PrepareBindingGIResults();
        }
        
        private void RPIRadioButtonChecked(object sender, RoutedEventArgs e)
        {
            //VMS.TPS.Script.StaticCollResult = null;
            PrepareBindingStructures();
        }

        private void GIApplyClicked(object sender, RoutedEventArgs e)
        {
            PrepareBindingGIResults();
        }

        private void CNApplyClicked(object sender, RoutedEventArgs e)
        {
            PrepareBindingCNResults();
        }

        private void FieldsCheckBoxes_Checked(object sender, RoutedEventArgs e)
        {
            VMS.TPS.Script.StaticCollResult = null;
            lstResults.ItemsSource = null;
            PrepareBindingStructures();
        }

        private void FieldsCheckBoxes_Unchecked(object sender, RoutedEventArgs e)
        {
            VMS.TPS.Script.StaticCollResult = null;
            lstResults.ItemsSource = null;
            PrepareBindingStructures();
        }

        private void ApplyClicked(object sender, RoutedEventArgs e)
        {
            PrepareBindingResults();
        }

    }
}