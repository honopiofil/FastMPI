using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows;



namespace VMS.TPS
{

    public class MyValidationRule : ValidationRule
    {
        public double Min { get; set; }
        public double Max { get; set; }

        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            ValidationResult vResult = ValidationResult.ValidResult;
            double parameter = 0;
            try
            {

                if (((string)value).Length > 0) //Check if there is a input in the textbox
                {
                    parameter = Double.Parse((String)value);
                }
            }
            catch (Exception e)
            {
                return new ValidationResult(false, "Illegal characters or " + e.Message);
            }

            if ((parameter < this.Min) || (parameter > this.Max))
            {
                return new ValidationResult(false, "Please enter value in the range: " + this.Min + " - " + this.Max + ".");
            }
            return vResult;
        }

    }


    public static class DvhExtensions
    {
        public static DoseValue GetDoseAtVolume(this PlanningItem pitem, Structure structure, double volume, VolumePresentation volumePresentation, DoseValuePresentation requestedDosePresentation)
        {
            if (pitem is PlanSetup)
            {
                return ((PlanSetup)pitem).GetDoseAtVolume(structure, volume, volumePresentation, requestedDosePresentation);
            }
            else
            {
                if (requestedDosePresentation != DoseValuePresentation.Absolute)
                    throw new ApplicationException("Only absolute dose supported for Plan Sums");
                DVHData dvh = pitem.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, volumePresentation, 0.001);
                return DvhExtensions.DoseAtVolume(dvh, volume);
            }
        }
        public static double GetVolumeAtDose(this PlanningItem pitem, Structure structure, DoseValue dose, VolumePresentation requestedVolumePresentation)
        {
            if (pitem is PlanSetup)
            {
                return ((PlanSetup)pitem).GetVolumeAtDose(structure, dose, requestedVolumePresentation);
            }
            else
            {
                DVHData dvh = pitem.GetDVHCumulativeData(structure, DoseValuePresentation.Absolute, requestedVolumePresentation, 0.001);
                return DvhExtensions.VolumeAtDose(dvh, dose.Dose);
            }
        }

        public static DoseValue DoseAtVolume(DVHData dvhData, double volume)
        {
            if (dvhData == null || dvhData.CurveData.Count() == 0)
                return DoseValue.UndefinedDose();
            double absVolume = dvhData.CurveData[0].VolumeUnit == "%" ? volume * dvhData.Volume * 0.01 : volume;
            if (volume < 0.0 || absVolume > dvhData.Volume)
                return DoseValue.UndefinedDose();

            DVHPoint[] hist = dvhData.CurveData;
            for (int i = 0; i < hist.Length; i++)
            {
                if (hist[i].Volume < volume)
                    return hist[i].DoseValue;
            }
            return DoseValue.UndefinedDose();
        }

        public static double VolumeAtDose(DVHData dvhData, double dose)
        {
            if (dvhData == null)
                return Double.NaN;

            DVHPoint[] hist = dvhData.CurveData;
            int index = (int)(hist.Length * dose / dvhData.MaxDose.Dose);
            if (index < 0 || index > hist.Length)
                return 0.0;//Double.NaN;
            else
                return hist[index].Volume;
        }
    }

    public class CoursePlan
    {
        public Course Course { get; set; }
        public PlanSetup Plan { get; set; }
        public string CoursePlanId { get; set; }
        public StructureSet StructureSet { get; set; }
        public bool IsCheckedPlan { get; set; }

        public CoursePlan(Course Course, PlanSetup Plan, string CoursePlanId, StructureSet StructureSet, bool IsCheckedPlan)
        {
            this.Course = Course;
            this.Plan = Plan;
            this.CoursePlanId = CoursePlanId;
            this.StructureSet = StructureSet;
            this.IsCheckedPlan = IsCheckedPlan;
        }
    }

    public class StructureTarget
    {
        public Structure Structure { get; set; }
        public bool IsCheckedStructure { get; set; }
        public bool Target { get; set; }
        public double MaxDose { get; set; }
        public double Weight { get; set; }

        public StructureTarget(Structure Structure, bool IsCheckedStructure, bool Target)
        {
            this.Structure = Structure;
            this.IsCheckedStructure = IsCheckedStructure;
            this.Target = Target;
            this.Weight = 1.00;
        }


        // trzeci konstruktor wykorzystywany w momencie kiedy uzytkownik wybral juz interesujace struktury 
        // i dla kazdej struktury okreslona jest maksymalna dawka sposrod wszystkich planow
        // lub tez kiedy zmienia wagi przypisywane danej strukturze
        public StructureTarget(Structure Structure, bool IsCheckedStructure, bool Target, double MaxDose, double Weight)
        {
            this.Structure = Structure;
            this.IsCheckedStructure = IsCheckedStructure;
            this.Target = Target;
            this.MaxDose = MaxDose;
            this.Weight = Weight;
        }
    }


    public class Result
    {
        public Course Course { get; set; }
        public PlanSetup Plan { get; set; }
        public string CoursePlanId { get; set; }
        public IEnumerable<Structure> Structures { get; set; }
        public double IndexResult { get; set; }

        public Result(Course Course, PlanSetup Plan, string CoursePlanId, IEnumerable<Structure> Structures, double IndexResult)
        {
            this.Course = Course;
            this.Plan = Plan;
            this.CoursePlanId = CoursePlanId;
            this.Structures = Structures;
            this.IndexResult = IndexResult;
        }
        // konstruktor do uproszczonej wersji prezentacji wynikow
        public Result(string CoursePlanId, double IndexResult)
        {
            this.CoursePlanId = CoursePlanId;
            this.IndexResult = IndexResult;
        }
    }



    // G£OWNA KLASA PROGRAMU
    public class Script
    {

        // KONSTRUKTOR
        public Script()
        {
        }

        // Najwiekszy problem mialem z utworzeniem objektu zawierajacego posrednia warstwe danych, tak by potem mozna bylo przypisac ja
        // zmiennej w code-behind oraz ostatecznie przygotowac wiazanie danych
        // Zastosowane rozwiazanie:
        // (1) utworzylem kolekcje CollPlanWithStruct
        // (2) utworzylem statyczne pole StaticCall
        // (3) w petli w metodzie PrepareData() do kolekcji CollPlanWithStruct dopisywane sa odpowiednie zmienne
        // (4) po skompletowaniu kolekcji jej zawartosc przypisywana jest polu StaticCall
        // (5) w code-behind wykorzystywana jest ta zawartosc poprzez konstrukcje VMS.TPS.Script.StaticColl
        // te zabiegi byly potrzebne bo zawartosci wlasciwosci niestatycznej nie moglem przywolac poza klasa w ktorej byla utworzona
        // a do wlasciwosci statycznej nie moglem dopisywac kolejnych elementow poprzez .Add
        // ostatecznie zamiast wlasciwosci StaticCall trzeba bylo (nie wiem czemu) poslugiwac sie polem statycznym

        // WLASCIWOSCI I POLA
        List<CoursePlan> CoursePlanList { get; set; }

        ObservableCollection<CoursePlan> CollCoursePlan { get; set; }
        public static ObservableCollection<CoursePlan> StaticCollCoursePlan;
        ObservableCollection<StructureTarget> CollStructureTarget { get; set; }
        public static ObservableCollection<StructureTarget> StaticCollStructureTarget;
        ObservableCollection<Result> CollResult { get; set; }
        public static ObservableCollection<Result> StaticCollResult;


        // METODY
        public void Execute(ScriptContext context, System.Windows.Window window)
        {

            CollCoursePlan = new ObservableCollection<CoursePlan>();
            IEnumerable<Course> courses = context.Patient.Courses;
            // PETLA PRZEBIEGAJACA PO WSZYSTKICH KURSACH I PLANACH W PACJENCIE I PRZYGOTOWUJACA
            // LISTE coursesplans ZAWIERAJACA:
            // (1) KURSY
            // (2) PLANY
            // (3) ZESTAWY STRUKTUR
            foreach (Course course in courses)
            {
                IEnumerable<PlanSetup> planSetups = course.PlanSetups;
                foreach (PlanSetup planSetup in planSetups)
                {
                    if (planSetup.Dose != null & planSetup.StructureSet != null)
                    {
                        string courseplanid = course.Id + " / " + planSetup.Id;
                        CollCoursePlan.Add(new CoursePlan(course, planSetup, courseplanid, planSetup.StructureSet, false));
                    }
                }
            }
            // PRZYPISANIE WYNIKU ZMIENNEJ STATYCZNEJ
            StaticCollCoursePlan = CollCoursePlan;
            StaticCollStructureTarget = null;
            StaticCollResult = null;

            //wywolanie metody MainControl() na sposob z esapi/Projects/Example_DVH
            var mainControl = new FastMPI.MainControl();

            // USTAWIENIA PARAMETROW GLOWNEGO OKNA APLIKACJI
            window.Content = mainControl;
            window.Background = System.Windows.Media.Brushes.Cornsilk;
            window.Height = 660;
            window.Width = 820;
            window.Title = "Multiple Planning Indices for " + context.Patient.Name;
        }


        public void PrepareStructures()
        {
            StaticCollStructureTarget = null;

            // sprawdzenie czy po odznaczeniu planow zostal chociaz jeden do analizy
            bool checkIfIsChecked = false;
            foreach (CoursePlan Plan in StaticCollCoursePlan)
            {
                if (Plan.IsCheckedPlan)
                {
                    checkIfIsChecked = true;
                    break;
                }
            }
            if (!checkIfIsChecked)
            {
                StaticCollStructureTarget = null;
                StaticCollResult = null; // wbrew mojemu rozumieniu wspoldzialania ObservableCllection i Binding nie czysci listy wynikow
                return;                  // dopisane 14.06.2018: trzeba zrobic lstResults.ItemsSource = null;
            }                            // Binding odnosi sie do wiazania wlasciwosci kontrolek z wlasciwosciami obiektow


            CollStructureTarget = new ObservableCollection<StructureTarget>();
            var StructuresTech = StructuresIntersect();
            foreach (Structure slIntersect in StructuresTech)
            {
                // sprawdzenie czy struktura nie jest pusta lub czy nie jest typu "Support" - jesli jest, nie wchodzi do kolekcji struktur do wyboru przez uzytkownika
                if (slIntersect.IsEmpty | slIntersect.DicomType == "SUPPORT")
                    continue;
                CollStructureTarget.Add(new StructureTarget(slIntersect, false, false));
            }

            StaticCollStructureTarget = CollStructureTarget;
        }



        //public static IEnumerable<string> StructuresIntersect()
        private IEnumerable<Structure> StructuresIntersect()
        {
            IEnumerable<CoursePlan> SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
            // Ponizsze chyba mozna zrobic prosciej - utworzyc zmienna przechowujaca StructureSet z wybranych planow i znalezc wspolny podzbior wszystkich Structure
            IEnumerable<Structure> Structures;
            List<IEnumerable<Structure>> ListOfStructureLists = new List<IEnumerable<Structure>>();
            foreach (CoursePlan plan in SelectedPlans)
            {
                Structures = plan.StructureSet.Structures;
                ListOfStructureLists.Add(Structures);
            }

            List<IEnumerable<Structure>> SL = ListOfStructureLists;
            IEnumerable<Structure> SLIntersect = SL.First();
            if (SL.Count() > 1)
            {
                IEnumerable<string> SLIntersectID = from structure in SLIntersect select structure.Id;

                foreach (IEnumerable<Structure> list in SL)
                {
                    IEnumerable<string> listID = from structure in list select structure.Id;
                    SLIntersectID = listID.Intersect(SLIntersectID);
                    SLIntersect = from structure in SLIntersect where SLIntersectID.Contains(structure.Id) select structure;
                }
            }
            return (SLIntersect);
        }


        public void CalculateGIResults()
        {

            var SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
            CollResult = new ObservableCollection<Result>();

            double GI;

            foreach (CoursePlan selectedPlan in SelectedPlans)
            {
                DoseValue prescribedDose = selectedPlan.Plan.TotalPrescribedDose;
                //double halfPrescribedDoseValue = prescribedDose.Dose / 2.0;
                DoseValue halfPrescribedDose = new DoseValue(prescribedDose.Dose / 2.0, prescribedDose.Unit);
                //Structure BODY = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == "BODY").First();
                //MessageBox.Show("Externals No: " + selectedPlan.StructureSet.Structures.Where(structure => structure.DicomType == "EXTERNAL").Count());
                Structure BODY = selectedPlan.StructureSet.Structures.Where(structure => structure.DicomType == "EXTERNAL").First();
                //Structure TARGET = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == selectedPlan.Plan.TargetVolumeID).First();
                //double TARGETvolume = TARGET.Volume;
                double BODYwithPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(BODY, prescribedDose, VolumePresentation.AbsoluteCm3);
                double BODYwithHalfPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(BODY, halfPrescribedDose, VolumePresentation.AbsoluteCm3);
                //double TARGETwithPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(TARGET, prescribedDose, VolumePresentation.AbsoluteCm3);
                //double dvalue1 = BODYwithPrescribedDoseVolume / TARGETvolume;
                //MessageBox.Show("Conformity Index: " + dvalue1.ToString());

                //MessageBox.Show("BodyVolume" + BODY.Volume.ToString());
                //MessageBox.Show("BODYwithPrescribedDoseVolume: " + BODYwithPrescribedDoseVolume.ToString());
                //MessageBox.Show("BODYwithHalfPrescribedDoseVolume: " + BODYwithHalfPrescribedDoseVolume.ToString());
                /*
                MessageBox.Show("BODYwithPrescribedDoseVolume: " + BODYwithPrescribedDoseVolume.ToString());
                double dvalue1 = (TARGETwithPrescribedDoseVolume / TARGETvolume);
                double dvalue2 = (TARGETwithPrescribedDoseVolume * BODYwithPrescribedDoseVolume);
                CN = dvalue1 * dvalue2;
                */
                if(BODYwithPrescribedDoseVolume < 0.001)
                {
                    MessageBox.Show("For the plan " + selectedPlan.CoursePlanId + " the prescription dose is not achieved. The plan will not be taken into account in results.");
                }
                else
                {
                    GI = BODYwithHalfPrescribedDoseVolume / BODYwithPrescribedDoseVolume;
                    GI = Math.Round(GI, 4);
                    CollResult.Add(new Result(selectedPlan.CoursePlanId, GI));
                }

            }

            StaticCollResult = CollResult;
        }


        public void CalculateCNResults()
        {

            var SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
            CollResult = new ObservableCollection<Result>();

            double CN;

            foreach (CoursePlan selectedPlan in SelectedPlans)
            {
                DoseValue prescribedDose = selectedPlan.Plan.TotalPrescribedDose;
                //Structure BODY = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == "BODY").First();
                Structure BODY = selectedPlan.StructureSet.Structures.Where(structure => structure.DicomType == "EXTERNAL").First();
                Structure TARGET = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == selectedPlan.Plan.TargetVolumeID).First();
                double TARGETvolume = TARGET.Volume;
                double BODYwithPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(BODY, prescribedDose, VolumePresentation.AbsoluteCm3);
                double TARGETwithPrescribedDoseVolume = selectedPlan.Plan.GetVolumeAtDose(TARGET, prescribedDose, VolumePresentation.AbsoluteCm3);
                double dvalue1 = BODYwithPrescribedDoseVolume / TARGETvolume;
                //MessageBox.Show("Conformity Index: " + dvalue1.ToString());
                /*
                MessageBox.Show("TARGETwithPrescribedDoseVolume: " + TARGETwithPrescribedDoseVolume.ToString());
                MessageBox.Show("TARGETvolume: " + TARGETvolume.ToString());
                MessageBox.Show("BODYwithPrescribedDoseVolume: " + BODYwithPrescribedDoseVolume.ToString());
                double dvalue1 = (TARGETwithPrescribedDoseVolume / TARGETvolume);
                double dvalue2 = (TARGETwithPrescribedDoseVolume * BODYwithPrescribedDoseVolume);
                CN = dvalue1 * dvalue2;
                */
                CN = (TARGETwithPrescribedDoseVolume / TARGETvolume) * (TARGETwithPrescribedDoseVolume / BODYwithPrescribedDoseVolume);
                CN = Math.Round(CN, 4);
                CollResult.Add(new Result(selectedPlan.CoursePlanId, CN));
            }

            StaticCollResult = CollResult;
        }



        public void CalculateResults(bool AreChangedWeights)
        {
            // czesc pierwsza w ktorej znajdowane sa maksymalne dawki dla kazdej interesujacej struktury sposrod wszystkich interesujacych planow
            ObservableCollection<StructureTarget> StructuresWithMaxDose = new ObservableCollection<StructureTarget>();
            var SelectedPlans = StaticCollCoursePlan.Where(plan => plan.IsCheckedPlan == true);
            var SelectedStructures = StaticCollStructureTarget.Where(structure => structure.IsCheckedStructure == true);

            double maxDose;

            foreach (StructureTarget selectedStructure in SelectedStructures)
            {

                string structureID = selectedStructure.Structure.Id; // dopisane zeby dla kazdego planu bral odpowiednia strukture na podstawie nazwy,
                                                                     // nawet jesli inne paramtery struktury nie zgadzaja sie
                maxDose = 0.0;
                foreach (CoursePlan selectedPlan in SelectedPlans)
                {

                    // dopisane zeby dla kazdego planu bral odpowiednia strukture na podstawie nazwy,
                    // nawet jesli inne paramtery struktury nie zgadzaja sie
                    Structure selectedStructureInSelectedPlan = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == structureID).First();
                    DVHData dvhData = selectedPlan.Plan.GetDVHCumulativeData(selectedStructureInSelectedPlan, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                    maxDose = maxDose > dvhData.MaxDose.Dose ? maxDose : dvhData.MaxDose.Dose;
                    // modul na razie wylaczony - porownuje maksmalna dawke dla targetu z dawka przepisana przez lekarza
                    /*
                    if (selectedStructure.Target)
                    {
                        prescribedDose = selectedPlan.Plan.TotalPrescribedDose.Dose;
                        maxDose = maxDose > prescribedDose ? maxDose : prescribedDose;
                    }
                    */
                }
                StructuresWithMaxDose.Add(new StructureTarget(selectedStructure.Structure, selectedStructure.IsCheckedStructure, selectedStructure.Target, maxDose, selectedStructure.Weight));
            }


            // czesc druga w ktorej obliczane sa czastkowe iRPI dla kazdej struktury, a nastepnie RPI dla calego planu
            CollResult = new ObservableCollection<Result>();

            bool target;
            double RPI;
            double stdDev;
            double iRPI;


            int Max = SelectedPlans.Count() * StructuresWithMaxDose.Count();

            int planCounter = 0;
            foreach (CoursePlan selectedPlan in SelectedPlans)
            {
                //RPI = 0.0; // wersja dla dodawania
                RPI = 1.0;
                int structureCounter = 0;
                planCounter++;

                foreach (StructureTarget selectedStructure in StructuresWithMaxDose)
                {
                    structureCounter++;
                    string structureID = selectedStructure.Structure.Id;
                    Structure selectedStructureInSelectedPlan = selectedPlan.StructureSet.Structures.Where(structure => structure.Id == structureID).First();
                    DVHData dvhData = selectedPlan.Plan.GetDVHCumulativeData(selectedStructureInSelectedPlan, DoseValuePresentation.Absolute, VolumePresentation.Relative, 0.1);
                    DVHPoint[] curveData = dvhData.CurveData;
                    target = selectedStructure.Target;
                    stdDev = target ? dvhData.StdDev : 0.0;

                    if (!AreChangedWeights)
                    {
                        iRPI = Calculate_iRPI(curveData, selectedStructure.MaxDose, target, stdDev);
                        RPI = RPI * iRPI;
                    }
                    else
                    {
                        double weight = selectedStructure.Weight;
                        iRPI = Calculate_iRPI_ChangedWeights(curveData, selectedStructure.MaxDose, target, stdDev, weight);
                        RPI = RPI * iRPI;

                    }

                    //RPI = RPI + iRPI; // Przygotowane na okazje przelaczenia na wzor z suma

                }

                RPI = Math.Pow(RPI, 1.0 / (double)SelectedStructures.Count()); // Przygotowane na okazje przelaczenia na wzor z iloczynem 
                                                                               // (pozostaje do ustalenia czy we wzorze powinno byc n + k czy n * k)
                RPI = Math.Round(RPI, 4);
                CollResult.Add(new Result(selectedPlan.CoursePlanId, RPI));
            }

            StaticCollResult = CollResult;
        }



        private double Calculate_iRPI(DVHPoint[] curveData, double maxDoseInPlan, bool target, double stdDev)
        {
            //PP moj fragment dotyczacy obliczenia wzglednego wypelnienia DVH
            int N = curveData.Length;
            double[,] dvhCurve = new double[2, N];
            for (int i = 0; i < N; i++)
            {
                dvhCurve[0, i] = curveData[i].DoseValue.Dose;
                dvhCurve[1, i] = curveData[i].Volume;
            }
            double AUC = Integrate(N, dvhCurve);
            double AUC_Dose_Volume_ratio = AUC / maxDoseInPlan / 100.0; // podzielone przez 100 ze wzgledu na wartosc Volume wyrazona w procentach

            double iRPI = target ? AUC_Dose_Volume_ratio * (1.0 - stdDev / maxDoseInPlan) : 1 - AUC_Dose_Volume_ratio;

            return (iRPI);
        }

        // wersja dla zmienionych wag struktur
        private double Calculate_iRPI_ChangedWeights(DVHPoint[] curveData, double maxDoseInPlan, bool target, double stdDev, double weight)
        {
            //PP moj fragment dotyczacy obliczenia wzglednego wypelnienia DVH
            int N = curveData.Length;
            double[,] dvhCurve = new double[2, N];
            for (int i = 0; i < N; i++)
            {
                dvhCurve[0, i] = curveData[i].DoseValue.Dose;
                dvhCurve[1, i] = curveData[i].Volume;
            }
            double AUC = Integrate(N, dvhCurve);
            double AUC_Dose_Volume_ratio = AUC / maxDoseInPlan / 100.0; // podzielone przez 100 ze wzgledu na wartosc Volume wyrazona w procentach

            double iRPI = target ? (AUC_Dose_Volume_ratio * weight * (1.0 - stdDev / maxDoseInPlan)) : ((1 - weight * AUC_Dose_Volume_ratio));

            return (iRPI);
        }

        //PP moja metoda obliczajaca pole pod krzywa DVH
        public static double Integrate(int N, double[,] y)
        {
            double sum = (y[1, 0] + y[1, N - 1]) / 2;
            for (int i = 1; i < N - 1; i++)
            {
                sum += y[1, i];
            }
            double h = (y[0, N - 1] - y[0, 0]) / (double)N;
            return h * sum;
        }
    }
}
