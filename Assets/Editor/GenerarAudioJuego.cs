using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SINTETIZADOR DE AUDIO DEL JUEGO.
///
/// Genera por codigo los sonidos que faltaban (o sonaban a marcador de posicion):
///   - Disparos por tipo de arma (rifle, pistola, escopeta, subfusil, sniper).
///   - Recargas distintas para rifle y pistola (cargador fuera / dentro / cerrojo).
///   - Voz del zombi: grunido, ataque, dano y muerte.
///   - Respiro final del jugador al morir.
///   - Cuchillo: silbido del tajo y golpe en carne.
///
/// Todo son muestras de 16 bits mono a 44,1 kHz escritas como .wav dentro de Assets.
/// Se puede volver a ejecutar cuando se quiera: sobreescribe los mismos archivos.
///
/// Menu: Aura -> Generar audios de juego
/// </summary>
public static class GenerarAudioJuego
{
    private const int SR = 44100;

    private const string DirArmas = "Assets/Audio/Armas";
    private const string DirZombi = "Assets/Audio/Zombi";
    private const string DirJugador = "Assets/Audio/Jugador";

    [MenuItem("Aura/Generar audios de juego")]
    public static void GenerarTodo()
    {
        Directory.CreateDirectory(DirArmas);
        Directory.CreateDirectory(DirZombi);
        Directory.CreateDirectory(DirJugador);

        // ---------------- DISPAROS ----------------
        // Un disparo real son tres cosas a la vez: el golpe seco del mecanismo, la
        // detonacion (aire) y la cola de la sala. Cada arma cambia el peso de cada parte.
        Guardar(DirArmas + "/shot_rifle.wav", Disparo(11, 0.75f,
            tauCuerpo: 0.055f, corteCuerpo: 2600f, hzGrave: 78f, nivelGrave: 0.85f,
            nivelCrack: 1.10f, tauCola: 0.20f, corteCola: 900f, nivelCola: 0.55f, reverb: 0.34f));

        Guardar(DirArmas + "/shot_pistol.wav", Disparo(23, 0.55f,
            tauCuerpo: 0.040f, corteCuerpo: 3400f, hzGrave: 105f, nivelGrave: 0.70f,
            nivelCrack: 1.25f, tauCola: 0.14f, corteCola: 1200f, nivelCola: 0.42f, reverb: 0.26f));

        Guardar(DirArmas + "/shot_shotgun.wav", Disparo(37, 0.95f,
            tauCuerpo: 0.085f, corteCuerpo: 1700f, hzGrave: 58f, nivelGrave: 1.10f,
            nivelCrack: 0.90f, tauCola: 0.28f, corteCola: 700f, nivelCola: 0.70f, reverb: 0.40f));

        Guardar(DirArmas + "/shot_smg.wav", Disparo(41, 0.42f,
            tauCuerpo: 0.030f, corteCuerpo: 3800f, hzGrave: 130f, nivelGrave: 0.55f,
            nivelCrack: 1.15f, tauCola: 0.10f, corteCola: 1400f, nivelCola: 0.34f, reverb: 0.20f));

        Guardar(DirArmas + "/shot_sniper.wav", Disparo(53, 1.30f,
            tauCuerpo: 0.070f, corteCuerpo: 2200f, hzGrave: 66f, nivelGrave: 1.05f,
            nivelCrack: 1.35f, tauCola: 0.45f, corteCola: 800f, nivelCola: 0.75f, reverb: 0.52f));

        // ---------------- RECARGAS ----------------
        Guardar(DirArmas + "/reload_rifle.wav", RecargaRifle(61));
        Guardar(DirArmas + "/reload_pistol.wav", RecargaPistola(67));
        Guardar(DirArmas + "/empty_click.wav", ClickSeco(71));

        // ---------------- ZOMBI ----------------
        Guardar(DirZombi + "/zombie_growl_1.wav", Grunido(101, 1.8f, 92f, 74f, 0.55f, 0.9f));
        Guardar(DirZombi + "/zombie_growl_2.wav", Grunido(113, 2.1f, 78f, 62f, 0.70f, 1.1f));
        Guardar(DirZombi + "/zombie_attack.wav", Grunido(127, 0.85f, 150f, 96f, 0.85f, 1.3f));
        Guardar(DirZombi + "/zombie_hurt.wav", Grunido(139, 0.55f, 175f, 130f, 0.90f, 1.4f));
        Guardar(DirZombi + "/zombie_death.wav", Grunido(149, 2.4f, 110f, 48f, 0.65f, 1.0f));

        // ---------------- JUGADOR ----------------
        Guardar(DirJugador + "/respiro_final.wav", RespiroFinal(211, 2.6f));

        // ---------------- CUCHILLO ----------------
        Guardar(DirArmas + "/knife_swing.wav", Tajada(301));
        Guardar(DirArmas + "/knife_hit.wav", GolpeCarne(307));

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log("[Audio] Sonidos generados en " + DirArmas + ", " + DirZombi + " y " + DirJugador);
    }

    // =====================================================================
    // DISPAROS
    // =====================================================================

    private static float[] Disparo(int semilla, float dur, float tauCuerpo, float corteCuerpo,
                                   float hzGrave, float nivelGrave, float nivelCrack,
                                   float tauCola, float corteCola, float nivelCola, float reverb)
    {
        System.Random rnd = new System.Random(semilla);
        int n = Mathf.CeilToInt(dur * SR);
        float[] o = new float[n];

        Biquad cuerpo = Biquad.LowPass(corteCuerpo, 0.75f);
        Biquad cola = Biquad.BandPass(corteCola, 1.15f);
        float fase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);

            // 1. Detonacion: ruido filtrado que se apaga muy rapido.
            float s = cuerpo.Process(ruido) * Exp(t, tauCuerpo) * 2.2f;

            // 2. Transitorio: el "chasquido" de los primeros milisegundos.
            s += ruido * Exp(t, 0.0035f) * nivelCrack;

            // 3. Golpe grave: seno que cae de tono (le da cuerpo de arma de fuego).
            float f = hzGrave * (1f + 1.4f * Exp(t, 0.045f));
            fase += 2f * Mathf.PI * f / SR;
            s += Mathf.Sin(fase) * Exp(t, 0.10f) * nivelGrave;

            // 4. Cola de la sala: ruido con mucho mas tiempo de caida.
            s += cola.Process(ruido) * Exp(t, tauCola) * nivelCola;

            // 5. Un pelin de saturacion: los disparos suenan "duros", no limpios.
            o[i] = (float)Math.Tanh(s * 1.35f);
        }

        AplicarReverb(o, reverb);
        SuavizarFinal(o, 0.25f);
        return o;
    }

    // =====================================================================
    // RECARGAS
    // =====================================================================

    private static float[] RecargaRifle(int semilla)
    {
        float dur = 1.85f;
        float[] o = new float[Mathf.CeilToInt(dur * SR)];

        AgregarGolpe(o, 0.12f, 0.16f, 210f, 0.85f, semilla + 1);   // liberar cargador
        AgregarGolpe(o, 0.34f, 0.12f, 150f, 0.55f, semilla + 2);   // cargador sale
        AgregarGolpe(o, 0.78f, 0.20f, 175f, 1.00f, semilla + 3);   // cargador entra
        AgregarGolpe(o, 1.02f, 0.09f, 320f, 0.45f, semilla + 4);   // palma golpeando
        AgregarGolpe(o, 1.30f, 0.22f, 430f, 0.90f, semilla + 5);   // cerrojo atras
        AgregarGolpe(o, 1.44f, 0.18f, 380f, 1.00f, semilla + 6);   // cerrojo adelante

        AplicarReverb(o, 0.16f);
        SuavizarFinal(o, 0.3f);
        return o;
    }

    private static float[] RecargaPistola(int semilla)
    {
        float dur = 1.35f;
        float[] o = new float[Mathf.CeilToInt(dur * SR)];

        AgregarGolpe(o, 0.10f, 0.14f, 190f, 0.70f, semilla + 1);   // pestillo
        AgregarGolpe(o, 0.30f, 0.11f, 140f, 0.50f, semilla + 2);   // cargador fuera
        AgregarGolpe(o, 0.68f, 0.18f, 210f, 1.00f, semilla + 3);   // cargador dentro
        AgregarGolpe(o, 0.98f, 0.16f, 520f, 1.05f, semilla + 4);   // corredera

        AplicarReverb(o, 0.14f);
        SuavizarFinal(o, 0.3f);
        return o;
    }

    private static float[] ClickSeco(int semilla)
    {
        float dur = 0.18f;
        float[] o = new float[Mathf.CeilToInt(dur * SR)];
        AgregarGolpe(o, 0.0f, 0.09f, 700f, 1.0f, semilla);
        SuavizarFinal(o, 0.2f);
        return o;
    }

    /// <summary>Un "clac" metalico: ruido con resonancia + un toque de tono.</summary>
    private static void AgregarGolpe(float[] o, float inicio, float durGolpe, float freq, float nivel, int semilla)
    {
        System.Random rnd = new System.Random(semilla);
        Biquad metal = Biquad.BandPass(freq, 7f);
        Biquad cuerpo = Biquad.LowPass(freq * 2.2f, 0.7f);

        int i0 = Mathf.FloorToInt(inicio * SR);
        int n = Mathf.CeilToInt(durGolpe * SR);
        float fase = 0f;

        for (int k = 0; k < n; k++)
        {
            int i = i0 + k;
            if (i < 0 || i >= o.Length) continue;

            float t = (float)k / SR;
            float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);

            float metalico = metal.Process(ruido) * Exp(t, 0.010f) * 2.4f;
            float golpe = cuerpo.Process(ruido) * Exp(t, 0.020f) * 1.6f;

            fase += 2f * Mathf.PI * freq / SR;
            float tono = Mathf.Sin(fase) * Exp(t, 0.016f) * 0.45f;

            o[i] += (metalico + golpe + tono) * nivel;
        }
    }

    // =====================================================================
    // VOZ DEL ZOMBI (grunidos)
    // =====================================================================

    /// <summary>
    /// Un grunido se hace con una "voz" (onda de diente de sierra que cae de tono) pasada
    /// por dos resonancias (formantes) y ensuciada con aire. El vibrato y la aspereza son
    /// lo que lo hace sonar animal y no a sintetizador.
    /// </summary>
    private static float[] Grunido(int semilla, float dur, float fInicio, float fFin, float aspereza, float aire)
    {
        System.Random rnd = new System.Random(semilla);
        int n = Mathf.CeilToInt(dur * SR);
        float[] o = new float[n];

        Biquad formante1 = Biquad.BandPass(430f, 3.2f);
        Biquad formante2 = Biquad.BandPass(1150f, 4.5f);
        Biquad soplo = Biquad.BandPass(620f, 1.1f);

        float fase = 0f;
        float deriva = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float u = t / dur;

            // Tono: cae de fInicio a fFin, con vibrato y una deriva aleatoria lenta.
            deriva += ((float)rnd.NextDouble() - 0.5f) * 0.9f / SR;
            float f = Mathf.Lerp(fInicio, fFin, u);
            f *= 1f + 0.035f * Mathf.Sin(2f * Mathf.PI * 5.3f * t);
            f *= 1f + deriva;

            fase += 2f * Mathf.PI * Mathf.Max(25f, f) / SR;
            float ciclo = fase / (2f * Mathf.PI);
            float diente = 2f * (ciclo - Mathf.Floor(ciclo + 0.5f));       // -1..1

            // Aspereza: se mezcla ruido con la voz (una garganta rota).
            float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float fuente = Mathf.Lerp(diente, ruido, aspereza * 0.35f);

            float voz = formante1.Process(fuente) * 1.7f + formante2.Process(fuente) * 0.8f;
            voz += soplo.Process(ruido) * 0.45f * aire;

            // Envolvente: sube rapido, se sostiene y cae. Con temblor.
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.75f));
            env *= 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 7.7f * t);
            env *= Mathf.Clamp01(t * 40f);                                  // ataque suave
            env *= Mathf.Clamp01((dur - t) * 8f);                           // sin corte seco

            o[i] = (float)Math.Tanh(voz * env * 1.4f);
        }

        AplicarReverb(o, 0.12f);
        SuavizarFinal(o, 0.35f);
        return o;
    }

    // =====================================================================
    // RESPIRACION FINAL DEL JUGADOR
    // =====================================================================

    /// <summary>
    /// Jadeo agónico: una inhalacion corta, una exhalacion larga y quebrada (con un
    /// estertor grave) y un ultimo soplo casi sin fuerza.
    /// </summary>
    private static float[] RespiroFinal(int semilla, float dur)
    {
        System.Random rnd = new System.Random(semilla);
        int n = Mathf.CeilToInt(dur * SR);
        float[] o = new float[n];

        Biquad pecho = Biquad.BandPass(340f, 1.4f);
        Biquad garganta = Biquad.BandPass(1250f, 1.8f);

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);

            // Tramo 0,00-0,35 s: inhalacion (sube de golpe).
            float inhalacion = 0f;
            if (t < 0.40f) inhalacion = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.40f)) * 0.55f;

            // Tramo 0,35-2,10 s: exhalacion larga que se va apagando.
            float exhalacion = 0f;
            if (t > 0.35f && t < 2.10f)
            {
                float u = (t - 0.35f) / 1.75f;
                exhalacion = Mathf.Pow(1f - u, 1.4f) * (0.55f + 0.45f * Mathf.Sin(Mathf.PI * u));
            }

            // Tramo 2,10-2,60 s: ultimo soplo.
            float ultimo = 0f;
            if (t >= 2.10f && t < 2.60f)
            {
                float u = (t - 2.10f) / 0.50f;
                ultimo = Mathf.Sin(Mathf.PI * u) * 0.35f;
            }

            // Estertor: temblor grave mientras sale el aire.
            float estertor = 1f;
            if (exhalacion > 0.01f) estertor = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 23f * t);

            float aire = pecho.Process(ruido) * 2.0f + garganta.Process(ruido) * 1.2f;
            float s = aire * (inhalacion * 0.8f + exhalacion * estertor + ultimo);

            o[i] = (float)Math.Tanh(s * 1.2f);
        }

        AplicarReverb(o, 0.10f);
        SuavizarFinal(o, 0.5f);
        return o;
    }

    // =====================================================================
    // CUCHILLO
    // =====================================================================

    /// <summary>Silbido del tajo: ruido con el filtro barriendo hacia arriba y cayendo.</summary>
    private static float[] Tajada(int semilla)
    {
        System.Random rnd = new System.Random(semilla);
        float dur = 0.42f;
        int n = Mathf.CeilToInt(dur * SR);
        float[] o = new float[n];

        Biquad barrido = Biquad.BandPass(900f, 1.6f);

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float u = t / dur;

            // Se recoloca el filtro cada 16 muestras: barrido de 700 Hz a 2600 Hz.
            if (i % 16 == 0) barrido = Biquad.BandPass(Mathf.Lerp(700f, 2600f, u), 1.6f);

            float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.6f));
            o[i] = barrido.Process(ruido) * env * 2.2f;
        }

        SuavizarFinal(o, 0.4f);
        return o;
    }

    /// <summary>Golpe en carne: apagado, grave y breve.</summary>
    private static float[] GolpeCarne(int semilla)
    {
        System.Random rnd = new System.Random(semilla);
        float dur = 0.30f;
        int n = Mathf.CeilToInt(dur * SR);
        float[] o = new float[n];

        Biquad grave = Biquad.LowPass(420f, 0.9f);
        Biquad chasquido = Biquad.BandPass(1500f, 2.5f);
        float fase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float ruido = (float)(rnd.NextDouble() * 2.0 - 1.0);

            float cuerpo = grave.Process(ruido) * Exp(t, 0.055f) * 2.6f;
            float chas = chasquido.Process(ruido) * Exp(t, 0.018f) * 1.2f;

            fase += 2f * Mathf.PI * 95f / SR;
            float golpe = Mathf.Sin(fase) * Exp(t, 0.045f) * 0.8f;

            o[i] = (float)Math.Tanh((cuerpo + chas + golpe) * 1.3f);
        }

        SuavizarFinal(o, 0.4f);
        return o;
    }

    // =====================================================================
    // UTILIDADES DE DSP
    // =====================================================================

    private static float Exp(float t, float tau)
    {
        return (float)Math.Exp(-t / Mathf.Max(0.0001f, tau));
    }

    /// <summary>Reverb barata: cuatro lineas de retardo con realimentacion.</summary>
    private static void AplicarReverb(float[] o, float mezcla)
    {
        if (mezcla <= 0f) return;

        float[] retardos = { 0.023f, 0.031f, 0.043f, 0.059f };
        for (int k = 0; k < retardos.Length; k++)
        {
            int d = Mathf.RoundToInt(retardos[k] * SR);
            if (d <= 0 || d >= o.Length) continue;
            float g = mezcla * (0.62f - 0.09f * k);
            if (g <= 0f) continue;

            for (int i = d; i < o.Length; i++)
                o[i] += o[i - d] * g;
        }
    }

    /// <summary>Rampa final para que el archivo no acabe con un chasquido.</summary>
    private static void SuavizarFinal(float[] o, float segundos)
    {
        int n = Mathf.Min(o.Length, Mathf.CeilToInt(segundos * SR));
        if (n <= 1) return;
        for (int i = 0; i < n; i++)
            o[o.Length - 1 - i] *= (float)i / n;
    }

    /// <summary>Escribe el array de muestras como .wav PCM 16 bits mono y normaliza el pico.</summary>
    private static void Guardar(string ruta, float[] datos, float pico = 0.94f)
    {
        float max = 0.0001f;
        for (int i = 0; i < datos.Length; i++) max = Mathf.Max(max, Mathf.Abs(datos[i]));
        float escala = pico / max;

        int dataSize = datos.Length * 2;
        using (FileStream fs = new FileStream(ruta, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataSize);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);        // PCM
            w.Write((short)1);        // mono
            w.Write(SR);
            w.Write(SR * 2);          // bytes por segundo
            w.Write((short)2);        // alineacion de bloque
            w.Write((short)16);       // bits por muestra
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataSize);

            for (int i = 0; i < datos.Length; i++)
            {
                int v = Mathf.RoundToInt(Mathf.Clamp(datos[i] * escala, -1f, 1f) * 32767f);
                w.Write((short)v);
            }
        }
        Debug.Log("[Audio] " + ruta + " (" + datos.Length + " muestras)");
    }

    /// <summary>Filtro biquad (RBJ cookbook). Es una struct para que Process() actualice el estado.</summary>
    private struct Biquad
    {
        public float b0, b1, b2, a1, a2;
        public float x1, x2, y1, y2;

        public float Process(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            return y;
        }

        public static Biquad LowPass(float freq, float q)
        {
            float w = 2f * Mathf.PI * Mathf.Clamp(freq, 20f, SR * 0.45f) / SR;
            float cw = Mathf.Cos(w);
            float alpha = Mathf.Sin(w) / (2f * Mathf.Max(0.05f, q));
            float a0 = 1f + alpha;

            Biquad b = new Biquad();
            b.b0 = ((1f - cw) * 0.5f) / a0;
            b.b1 = (1f - cw) / a0;
            b.b2 = b.b0;
            b.a1 = (-2f * cw) / a0;
            b.a2 = (1f - alpha) / a0;
            return b;
        }

        public static Biquad BandPass(float freq, float q)
        {
            float w = 2f * Mathf.PI * Mathf.Clamp(freq, 20f, SR * 0.45f) / SR;
            float cw = Mathf.Cos(w);
            float alpha = Mathf.Sin(w) / (2f * Mathf.Max(0.05f, q));
            float a0 = 1f + alpha;

            Biquad b = new Biquad();
            b.b0 = alpha / a0;
            b.b1 = 0f;
            b.b2 = -alpha / a0;
            b.a1 = (-2f * cw) / a0;
            b.a2 = (1f - alpha) / a0;
            return b;
        }
    }
}
