using UnityEngine;

/// <summary>
/// Marca una zona del cuerpo que recibe mas (o menos) dano. Se coloca en el collider
/// de la cabeza, de un brazo, etc. El arma comprueba este componente en el punto de
/// impacto, asi que el dano depende de DONDE le des.
/// </summary>
public class ZonaDeGolpe : MonoBehaviour
{
    [Tooltip("Multiplicador de dano de esta zona. Cabeza 2.5 = casi el triple. Extremidad 0.75.")]
    [SerializeField] private float multiplicador = 2.5f;

    [Tooltip("Solo para depurar: CABEZA, BRAZO, PIERNA...")]
    [SerializeField] private string etiqueta = "CABEZA";

    public float Multiplicador => multiplicador;
    public string Etiqueta => etiqueta;

    /// <summary>Permite configurarla desde codigo (por ejemplo al crear la zona de cabeza).</summary>
    public void Configurar(float nuevoMultiplicador, string nuevaEtiqueta)
    {
        multiplicador = Mathf.Max(0.05f, nuevoMultiplicador);
        etiqueta = nuevaEtiqueta;
    }
}
