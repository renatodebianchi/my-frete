import { apiFetch } from './client';

export type LatLng = { lat: number; lng: number };

export type PriceEstimate = {
  amount: number;
  currency: string;
  distanceKm: number;
  distanceSource: 'routed' | 'geodesic_fallback';
  isEstimate: boolean;
};

export type TransportRequest = {
  id: string;
  status:
    | 'draft'
    | 'searching'
    | 'hired'
    | 'awaiting_schedule_decision'
    | 'scheduled_searching'
    | 'scheduled'
    | 'completed'
    | 'unfulfilled'
    | 'cancelled';
  kind: 'immediate' | 'scheduled';
  estimate: { amount: number; currency: string; distanceKm: number; distanceSource: string; isEstimate: boolean };
  items: { description: string; quantity: number }[];
  originAddress: string;
  destinationAddress: string;
  assignedProfessionalId: string | null;
  createdAt: string;
};

export type OfferInbox = {
  id: string;
  requestId: string;
  respondBy: string;
  summary: {
    originAddress: string;
    destinationAddress: string;
    distanceKm: number;
    estimatedWeightKg: number;
    estimatedAmount: number;
  };
};

export type Trip = {
  id: string;
  requestId: string;
  status: 'contratada' | 'em_andamento' | 'entregue' | 'confirmada' | 'contestada' | 'cancelada';
  agreedAmount: number;
  currency: string;
  deliveredAt: string | null;
  clientResponse: 'confirmada' | 'contestada' | null;
  paymentSettledOutsideApp: boolean;
};

type Address = { text: string; point: LatLng };
type ItemInput = { description: string; quantity: number };

export const freightApi = {
  estimate: (origin: Address, destination: Address, weightKg: number) =>
    apiFetch<PriceEstimate>('/pricing/estimate', {
      method: 'POST',
      body: JSON.stringify({ origin, destination, estimatedWeightKg: weightKg }),
    }),

  createRequest: (input: {
    items: ItemInput[];
    estimatedWeightKg: number;
    origin: Address;
    destination: Address;
    idempotencyKey: string;
  }) =>
    apiFetch<{ id: string }>('/requests', {
      method: 'POST',
      headers: { 'Idempotency-Key': input.idempotencyKey },
      body: JSON.stringify({
        items: input.items,
        estimatedWeightKg: input.estimatedWeightKg,
        origin: input.origin,
        destination: input.destination,
      }),
    }),

  getRequest: (id: string) => apiFetch<TransportRequest>(`/requests/${id}`),
  listRequests: () => apiFetch<{ items: TransportRequest[] }>('/requests'),
  cancelRequest: (id: string) => apiFetch<TransportRequest>(`/requests/${id}/cancel`, { method: 'POST' }),

  offersInbox: () => apiFetch<OfferInbox[]>('/offers/inbox'),
  acceptOffer: (id: string) =>
    apiFetch<{ tripId: string; requestId: string }>(`/offers/${id}/accept`, { method: 'POST' }),
  declineOffer: (id: string) => apiFetch<void>(`/offers/${id}/decline`, { method: 'POST' }),

  getTrip: (id: string) => apiFetch<Trip>(`/trips/${id}`),
  listTrips: () => apiFetch<{ items: Trip[] }>('/trips'),
  setAgreedAmount: (id: string, amount: number) =>
    apiFetch<Trip>(`/trips/${id}/agreed-amount`, { method: 'PATCH', body: JSON.stringify({ amount }) }),
  deliverTrip: (id: string) => apiFetch<Trip>(`/trips/${id}/deliver`, { method: 'POST' }),
  clientRespond: (id: string, response: 'confirm' | 'dispute', note?: string) =>
    apiFetch<Trip>(`/trips/${id}/client-response`, {
      method: 'POST',
      body: JSON.stringify({ response, note }),
    }),
  cancelTrip: (id: string) => apiFetch<Trip>(`/trips/${id}/cancel`, { method: 'POST' }),
};
