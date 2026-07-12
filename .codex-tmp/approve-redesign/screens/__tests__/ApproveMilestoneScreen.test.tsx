import { describe, expect, it, beforeEach, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ApproveMilestoneScreen from '../ApproveMilestoneScreen';
import { contractGetAPI } from '../../../../api/contractAPI/GET';
import { contractPostAPI } from '../../../../api/contractAPI/POST';
import type { MilestoneAttachment } from '../../../../types/models/Contract';
import { MilestoneStatus } from '../../../../types/models/Contract';

const navigateMock = vi.fn();

vi.mock('../../../../api/contractAPI/GET', () => ({
  contractGetAPI: {
    getContractById: vi.fn(),
    getMilestoneById: vi.fn(),
    getMilestoneAttachments: vi.fn(),
  },
}));

vi.mock('../../../../api/contractAPI/POST', () => ({
  contractPostAPI: {
    approveMilestone: vi.fn(),
    requestMilestoneRevision: vi.fn(),
  },
}));

vi.mock('../../../../app/providers/AppProvider', () => ({
  useApp: () => ({ user: { id: 'user-1' } }),
}));

vi.mock('../../../../shared/components/AppLayout', () => ({
  AppLayout: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));

vi.mock('../../../../shared/components/GigCoinAmount', () => ({
  GigCoinLogo: () => <span data-testid="gigcoin-logo" />,
}));

vi.mock('react-router', () => ({
  useParams: () => ({ contractId: 'contract-1', milestoneId: 'milestone-1' }),
  useNavigate: () => navigateMock,
}));

describe('ApproveMilestoneScreen', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useRealTimers();

    vi.mocked(contractGetAPI.getContractById).mockResolvedValue({
      success: true,
      statusCode: 200,
      message: 'Success',
      data: {
        contractsId: 'contract-1',
        jobPostsId: 'job-1',
        clientProfilesId: 'client-1',
        freelancerProfilesId: 'freelancer-1',
        title: 'Website Redesign',
        totalBudget: 1000,
        status: 7,
        createdAt: '2026-07-02T01:00:00.000Z',
      },
    });

    vi.mocked(contractGetAPI.getMilestoneById).mockResolvedValue({
      success: true,
      statusCode: 200,
      message: 'Success',
      data: {
        id: 'milestone-1',
        contract_id: 'contract-1',
        title: 'Final delivery',
        amount: 1000,
        due_date: '2026-07-10',
        status: MilestoneStatus.Submitted,
        paid_at: null,
      },
    });

    vi.mocked(contractGetAPI.getMilestoneAttachments).mockResolvedValue({
      success: true,
      statusCode: 200,
      message: 'Success',
      data: [],
    });
  });

  it('renders backend-style attachments without crashing', async () => {
    vi.mocked(contractGetAPI.getMilestoneAttachments).mockResolvedValue({
      success: true,
      statusCode: 200,
      message: 'Success',
      data: [
        {
          MilestoneAttachmentsId: 'attachment-1',
          MilestonesId: 'milestone-1',
          FileName: 'final-deliverable.pdf',
          FileUrl: 'https://example.com/final-deliverable.pdf',
        },
      ] as unknown as MilestoneAttachment[],
    });

    render(<ApproveMilestoneScreen />);

    expect(await screen.findByText('Submitted deliverables')).toBeInTheDocument();
    expect(screen.getByText('Attachment 1')).toBeInTheDocument();
  });

  it('navigates the footer action back to the workspace', async () => {
    const user = userEvent.setup();

    render(<ApproveMilestoneScreen />);

    const workspaceButton = await screen.findByRole('button', { name: /back to workspace/i });
    await user.click(workspaceButton);

    expect(navigateMock).toHaveBeenCalledWith('/workspace/contract-1');
  });

  it('renders a clear empty state when no deliverables are attached', async () => {
    render(<ApproveMilestoneScreen />);

    expect(await screen.findByText('Submitted deliverables')).toBeInTheDocument();
    expect(screen.getByText('No attached files')).toBeInTheDocument();
    expect(screen.getByText('0 files')).toBeInTheDocument();
  });

  it('requires a revision reason and enforces the 500 character limit', async () => {
    const user = userEvent.setup();
    render(<ApproveMilestoneScreen />);

    await user.click(await screen.findByRole('button', { name: /request revision send it back/i }));
    const submit = screen.getByRole('button', { name: /^request revision$/i });
    expect(submit).toBeDisabled();

    const reason = screen.getByLabelText(/what needs to be changed/i);
    await user.type(reason, 'Please update the authentication flow.');
    expect(submit).toBeEnabled();

    fireEvent.change(reason, { target: { value: 'a'.repeat(501) } });
    expect(screen.getByText('501/500')).toHaveClass('is-over');
    expect(submit).toBeDisabled();
  });

  it('redirects to the workspace after approving a milestone', async () => {
    vi.mocked(contractPostAPI.approveMilestone).mockResolvedValue({
      success: true,
      statusCode: 200,
      message: 'Success',
      data: {
        id: 'milestone-1',
        contract_id: 'contract-1',
        title: 'Final delivery',
        amount: 1000,
        due_date: '2026-07-10',
        status: MilestoneStatus.Approved,
        paid_at: null,
      },
    });

    render(<ApproveMilestoneScreen />);

    const approveOption = (await screen.findByText('Approve work')).closest('button');
    expect(approveOption).not.toBeNull();
    fireEvent.click(approveOption!);
    vi.useFakeTimers();
    fireEvent.click(screen.getByRole('button', { name: /approve milestone/i }));
    await act(async () => {
      await Promise.resolve();
    });
    act(() => {
      vi.advanceTimersByTime(2000);
    });

    expect(contractPostAPI.approveMilestone).toHaveBeenCalledWith('contract-1', 'milestone-1');
    expect(navigateMock).toHaveBeenCalledWith('/workspace/contract-1');
  });
});
