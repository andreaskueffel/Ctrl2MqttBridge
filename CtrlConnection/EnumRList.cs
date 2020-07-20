using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MqttBridge
{
    public enum RList
    {
        Cycle = 1,
        SetupSimu = 2,
        Loading = 3,

        Process = 11,
        YPosMeasValid = 440,
        YPosMeasStart = 448,
        YPosMeasEnd = 449,
        CMeasPos = 715,

        KeepAlive = 900,        //Kommunikationsüberwachung
        ProgCMD = 901,          //Programmanforderung durch HMI
        NCProgSub = 902,        //Aktuelles NC Programm: 1=Abfrage verzahnt, 34=Profilieren/Abrichten, 50=Honen |Programm fertig =+100 (134, 150)
        NCProgSubAck = 903,     //Bestätigung der HMI das gerechnet wurde: 3401=Werte für Abrichten stehen bereit, 3411=Werte wurden übernommen,...

        NCServer = 905,         //Analog zur Rexroth Kommunikation NC --> AdaptivHonServer
        ServerNC = 906,         //Analog zur Rexroth Kommunikation AdaptivHonServer --> NC
        HRICommMonitor = 907,     //HRI Kommunikation überwachen
        ToolLifeMax = 910,      //Berechnete Werkzeugstandzeit Honrad neu
        ToolLifeAct = 911,      //Aktuelle Werkzeugstandzeit Honrad (wird beim Abrichten neu geschrieben, Progr. 134)
        DresInterval = 912,     //Abrichtzyklus
        DresCountAct = 913,     //Restteile bis zum Abrichten (wird nach Abrichten (134) neu geschrieben
        PartCountHMI = 914,       //Werkstückzähler, wird durch NC hoch gezählt und kann über HMI resettet werden
        SPCBeforeAfterDres = 915,//SPC vor/nach Abrichten
        SPCCountMax = 916,        //SPC Zähler Sollwert
        SPCCountAct = 917,        //Aktueller SPC Zähler (wird durch NC runter gezählt)
        ToolLifeActSkv = 918,     //Werkstückzähler Skiving, wird durch NC runter gezählt
        ToolLifeMaxSkv = 919,     //Skiving Soll-Standzeit Werkzeug

        NewTool = 920,            //Gibt an ob ein neues, unverzahntes Werkzeug verbaut ist
        HeadProfAmount = 921,   //Kopfabrichtbetrag für ein neues, unverzahntes Werkzeug
        CutCount = 922,           //Zählt beim VSD die Schnitte hoch
        PartCountNOK = 923,       //NIO Teilezähler
        CCorrect = 930,           //Korrektur C-Achse (Live-Wert aus HMI)
        XCorrect = 931,           //Korrektur Y-Achse (Live-Wert aus HMI)
        BCorrect = 932,           //Korrektur AKW
        BCorrectX = 933,          //Korrektur AKW X-Richtung
        BCorrectZ = 934,         //Korrektur AKW Z-Richtung
        VerschlKompX = 935,      //Verschleißkompensation je Werkstück (Live-Wert)
        Simulation = 936,       //Simulation aktiv an NC
        HelixCorrect = 937,       //Schrägungswinkelkorrektur beim Skiving

        RckBallsizeRpTarget = 940, //Kugelmass Rohteil Soll
        RckBallsizeRpActual = 941,   //Kugelmass Rohteil Ist
        RckBallsizeFpTarget = 942,  //Kugelmass Fertigteil Soll
        RckBallsizeFpActual = 943,  //Kugelmass Fertigteil Ist

        StateWPMeas = 1915,       //Status Einmitten


        GrindingCorr1 = 6920,     //Durchmesser Korrektur Schleifen
        GrindingCorr2 = 6921,     //Durchmesser Korrektur Schleifen
        GrindingCorr3 = 6922,     //Durchmesser Korrektur Schleifen
        GrindingCorr4 = 6923,     //Durchmesser Korrektur Schleifen
        GrindingCorr5 = 6924,     //Durchmesser Korrektur Schleifen
        GrindingCorr6 = 6925,     //Durchmesser Korrektur Schleifen
        GrindingCorr7 = 6926,     //Durchmesser Korrektur Schleifen
        GrindingCorr8 = 6927,     //Durchmesser Korrektur Schleifen
        GrindingCorr9 = 6928,     //Durchmesser Korrektur Schleifen
        GrindingCorr10 = 6929,     //Durchmesser Korrektur Schleifen

        GrindingActDia = 6262,     //Aktueller Durchmesser Schleifscheibe
        GrindingActDressing = 6270,     //Aktueller Abrichtbetrag Schleifscheibe

        GrindingActDressingCounter = 6280,     //Aktueller Abrichtzaehler Zyklus
        GrindingActDressingMaxCounter = 6285,     //Aktueller Sollzyklus Aabrichten
        GrindingActDressingCounterTillDress = 6908,     //Restzähler Zyklen bis Abrichten
        GrindingDressAct = 6910,     //Abrichten
        GrindingNewTool = 6911,     //neue Schleifscheibe eingebaut

        GrindingDressingCounterToolAct = 6932,
        GrindingDressingCounterToolMax = 6931,
        GrindingDressingCounterToolRemaining = 6930,

        GrindingDressingCounterDresserAct = 6940,
        GrindingDressingCounterDresserMax = 6941,
        GrindingDressingCounterDresserRemaining = 6942,

        GrindingCounterWorkpieceAct = 6950,
        GrindingCounterWorkpieceMax = 6951,
        GrindingCounterWorkpieceRemaining = 6952,

    }
}
